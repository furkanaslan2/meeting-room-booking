import { apiRequest, clearToken, getToken } from "./api.js";

const form = document.querySelector("#room-filters");
const officeFilter = document.querySelector("#office-filter");
const capacityFilter = document.querySelector("#capacity-filter");
const equipmentFilter = document.querySelector("#equipment-filter");
const availableOnly = document.querySelector("#available-only");
const showInactive = document.querySelector("#show-inactive");
const inactiveOption = document.querySelector("#inactive-option");
const timeFields = document.querySelector("#time-fields");
const startTime = document.querySelector("#start-time");
const endTime = document.querySelector("#end-time");
const list = document.querySelector("#room-list");
const notice = document.querySelector("#room-notice");
const emptyMessage = document.querySelector("#empty-message");
const pagination = document.querySelector("#pagination");
const previousPage = document.querySelector("#previous-page");
const nextPage = document.querySelector("#next-page");
const searchButton = document.querySelector("#search-button");

const officeNames = new Map();
const pageSize = 4;
let page = 1;

function showError(message) {
  notice.textContent = message;
  notice.hidden = !message;
}

function addOption(select, value, label) {
  const option = document.createElement("option");
  option.value = String(value);
  option.textContent = label;
  select.append(option);
}

function dateInTurkey(date) {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit"
  }).formatToParts(date);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}`;
}

function updateTimeFields() {
  timeFields.hidden = !availableOnly.checked;
  startTime.required = availableOnly.checked;
  endTime.required = availableOnly.checked;
  inactiveOption.hidden = availableOnly.checked || inactiveOption.dataset.roleHidden === "true";
}

function createRoomCard(room) {
  const card = document.createElement("article");
  card.className = "room-card";

  const top = document.createElement("div");
  top.className = "room-card-top";
  const office = document.createElement("span");
  office.className = "room-office";
  office.textContent = officeNames.get(room.officeId) || `Ofis ${room.officeId}`;
  const status = document.createElement("span");
  status.className = room.isActive ? "room-status" : "room-status inactive";
  status.textContent = room.isActive ? "Aktif" : "Pasif";
  top.append(office, status);

  const heading = document.createElement("h3");
  heading.textContent = room.name;
  const details = document.createElement("p");
  details.className = "room-details";
  details.textContent = `${room.capacity} kişi · ${room.floor}. kat`;

  const equipment = document.createElement("div");
  equipment.className = "equipment-list";
  for (const item of room.equipment) {
    const badge = document.createElement("span");
    badge.textContent = item.name;
    equipment.append(badge);
  }
  if (room.equipment.length === 0) {
    const badge = document.createElement("span");
    badge.textContent = "Ekipman belirtilmedi";
    equipment.append(badge);
  }

  card.append(top, heading, details, equipment);
  return card;
}

function renderResults(result) {
  list.replaceChildren(...result.items.map(createRoomCard));
  emptyMessage.hidden = result.items.length > 0;
  const totalPages = Math.max(1, Math.ceil(result.totalCount / pageSize));
  document.querySelector("#result-count").textContent = `${result.totalCount} oda bulundu`;
  document.querySelector("#page-label").textContent = `${result.page} / ${totalPages}`;
  pagination.hidden = result.totalCount <= pageSize;
  previousPage.disabled = result.page <= 1;
  nextPage.disabled = result.page >= totalPages;
}

async function loadRooms() {
  showError("");
  list.replaceChildren();
  emptyMessage.hidden = true;
  pagination.hidden = true;
  previousPage.disabled = true;
  nextPage.disabled = true;
  document.querySelector("#result-count").textContent = "Odalar yükleniyor...";
  list.setAttribute("aria-busy", "true");
  searchButton.disabled = true;
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (officeFilter.value) params.set("officeId", officeFilter.value);
  if (capacityFilter.value) params.set("minCapacity", capacityFilter.value);
  if (equipmentFilter.value) params.set("equipmentId", equipmentFilter.value);

  let path = "/api/rooms";
  if (availableOnly.checked) {
    params.set("startsAt", `${startTime.value}:00+03:00`);
    params.set("endsAt", `${endTime.value}:00+03:00`);
    path += "/available";
  } else if (!showInactive.checked) {
    params.set("isActive", "true");
  }

  try {
    const result = await apiRequest(`${path}?${params}`);
    renderResults(result);
  } catch (error) {
    if (error.status === 401) {
      location.replace("/");
      return;
    }
    document.querySelector("#result-count").textContent = "";
    showError(error.message || "Odalar yüklenemedi.");
  } finally {
    list.setAttribute("aria-busy", "false");
    searchButton.disabled = false;
  }
}

form.addEventListener("submit", (event) => {
  event.preventDefault();
  page = 1;
  loadRooms();
});

availableOnly.addEventListener("change", updateTimeFields);
previousPage.addEventListener("click", () => { page -= 1; loadRooms(); });
nextPage.addEventListener("click", () => { page += 1; loadRooms(); });

document.querySelector("#sign-out").addEventListener("click", async (event) => {
  const button = event.currentTarget;
  button.disabled = true;
  try {
    await apiRequest("/api/auth/logout", { method: "POST" });
    clearToken();
    location.replace("/");
  } catch (error) {
    if (error.status === 401) {
      clearToken();
      location.replace("/");
    } else {
      showError(error.message || "Çıkış tamamlanamadı.");
      button.disabled = false;
    }
  }
});

async function initialize() {
  if (!getToken()) {
    location.replace("/");
    return;
  }

  const tomorrow = dateInTurkey(new Date(Date.now() + 24 * 60 * 60 * 1000));
  startTime.value = `${tomorrow}T10:00`;
  endTime.value = `${tomorrow}T11:00`;

  try {
    const user = await apiRequest("/api/auth/me");
    const roleNames = { Admin: "Yönetici", OfficeManager: "Ofis yöneticisi", Employee: "Çalışan" };
    document.querySelector("#user-role").textContent = roleNames[user.role] || user.role;
    inactiveOption.dataset.roleHidden = String(user.role === "Employee");
    updateTimeFields();

    const [offices, equipment] = await Promise.all([
      apiRequest("/api/offices?page=1&pageSize=100"),
      apiRequest("/api/equipment?page=1&pageSize=100")
    ]);
    for (const office of offices.items) {
      officeNames.set(office.id, office.name);
      addOption(officeFilter, office.id, office.name);
    }
    for (const item of equipment.items) addOption(equipmentFilter, item.id, item.name);
    await loadRooms();
  } catch (error) {
    if (error.status === 401) {
      location.replace("/");
      return;
    }
    showError(error.message || "Sayfa yüklenemedi.");
  }
}

initialize();
