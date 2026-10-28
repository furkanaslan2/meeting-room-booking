import { apiRequest, clearToken, getToken } from "./api.js";
import { applyRoleMenu } from "./role-menu.js";

const $ = (selector) => document.querySelector(selector);
const notice = $("#management-notice");
const officeForm = $("#office-form");
const equipmentForm = $("#equipment-form");
const roomForm = $("#room-form");
const roomOffice = $("#room-office");
const officeFilter = $("#room-filter-office");
const pageSize = 8;
const state = { user: null, offices: [], equipment: [], officeEdit: null, roomEdit: null, page: 1, usersPage: 1 };

function showNotice(message, kind = "error") {
  notice.textContent = message;
  notice.dataset.kind = kind;
  notice.hidden = !message;
  if (message) notice.scrollIntoView({ block: "nearest" });
}

function handleError(error) {
  if (error.status === 401) { clearToken(); location.replace("/"); return; }
  showNotice(error.message || "İşlem tamamlanamadı.");
}

async function allPages(path) {
  const items = [];
  for (let page = 1; ; page += 1) {
    const result = await apiRequest(`${path}?page=${page}&pageSize=100`);
    items.push(...result.items);
    if (items.length >= result.totalCount) return items;
  }
}

function option(value, label) {
  const item = document.createElement("option");
  item.value = String(value);
  item.textContent = label;
  return item;
}

function action(label, callback, destructive = false) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = `secondary-button${destructive ? " cancel-button" : ""}`;
  button.textContent = label;
  button.addEventListener("click", callback);
  return button;
}

function resetOfficeForm() {
  state.officeEdit = null;
  officeForm.reset();
  $("#office-submit").textContent = "Ofis ekle";
  $("#office-cancel").hidden = true;
}

function renderOffices() {
  const cards = state.offices.map((office) => {
    const card = document.createElement("article");
    card.className = "management-card";
    const heading = document.createElement("h3");
    heading.textContent = office.name;
    const city = document.createElement("p");
    city.textContent = office.city;
    const actions = document.createElement("div");
    actions.className = "management-actions";
    actions.append(
      action("Düzenle", () => {
        state.officeEdit = office.id;
        $("#office-name").value = office.name;
        $("#office-city").value = office.city;
        $("#office-submit").textContent = "Değişiklikleri kaydet";
        $("#office-cancel").hidden = false;
        officeForm.scrollIntoView({ block: "center" });
      }),
      action("Sil", async () => {
        if (!window.confirm(`${office.name} ofisini silmek istiyor musunuz? İçinde odalar varsa işlem reddedilir.`)) return;
        try {
          await apiRequest(`/api/offices/${office.id}`, { method: "DELETE" });
          if (state.officeEdit === office.id) resetOfficeForm();
          await loadOffices();
          await loadRooms();
          await loadUsers();
          showNotice("Ofis silindi.", "success");
        } catch (error) { handleError(error); }
      }, true)
    );
    card.append(heading, city, actions);
    return card;
  });
  $("#office-list").replaceChildren(...cards);
}

async function loadOffices() {
  state.offices = await allPages("/api/offices");
  const selectedOffice = roomOffice.value;
  const selectedFilter = officeFilter.value;
  const allowed = state.user.role === "Admin"
    ? state.offices : state.offices.filter((office) => office.id === state.user.officeId);
  roomOffice.replaceChildren(option("", "Ofis seçin"), ...allowed.map((office) => option(office.id, office.name)));
  officeFilter.replaceChildren(option("", "Tüm ofisler"), ...state.offices.map((office) => option(office.id, office.name)));
  if (allowed.some((office) => String(office.id) === selectedOffice)) roomOffice.value = selectedOffice;
  if (state.user.role !== "Admin") roomOffice.value = String(state.user.officeId);
  if (state.offices.some((office) => String(office.id) === selectedFilter)) officeFilter.value = selectedFilter;
  if (state.user.role === "Admin") renderOffices();
}

function renderEquipmentChoices() {
  const choices = state.equipment.map((item) => {
    const label = document.createElement("label");
    label.className = "checkbox-label";
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.value = String(item.id);
    label.append(checkbox, document.createTextNode(` ${item.name}`));
    return label;
  });
  $("#room-equipment").replaceChildren(...choices);
  $("#equipment-list").textContent = state.equipment.length
    ? state.equipment.map((item) => item.name).join(" · ") : "Henüz ekipman eklenmedi.";
}

async function loadEquipment() {
  const selected = new Set([...$("#room-equipment").querySelectorAll('input[type="checkbox"]:checked')]
    .map((input) => input.value));
  state.equipment = await allPages("/api/equipment");
  renderEquipmentChoices();
  for (const input of $("#room-equipment").querySelectorAll('input[type="checkbox"]')) {
    input.checked = selected.has(input.value);
  }
}

function renderUsers(result) {
  const names = { Admin: "Admin", OfficeManager: "Ofis Yöneticisi", Employee: "Çalışan" };
  const cards = result.items.map((user) => {
    const card = document.createElement("article");
    card.className = "management-card";
    const heading = document.createElement("h3");
    heading.textContent = user.fullName;
    const email = document.createElement("p");
    email.textContent = user.email;
    const current = document.createElement("p");
    current.textContent = `Mevcut rol: ${names[user.role] || user.role}`;
    card.append(heading, email, current);
    if (user.id === state.user.id) {
      const own = document.createElement("p");
      own.textContent = "Kendi rolünüz bu ekrandan değiştirilemez.";
      card.append(own);
      return card;
    }
    const form = document.createElement("form");
    form.className = "management-user-form";
    const roleField = document.createElement("div");
    roleField.className = "field";
    const roleLabel = document.createElement("label");
    roleLabel.textContent = "Yeni rol";
    const roleSelect = document.createElement("select");
    roleSelect.setAttribute("aria-label", `${user.fullName} için yeni rol`);
    roleSelect.replaceChildren(...Object.entries(names).map(([value, label]) => option(value, label)));
    roleSelect.value = user.role;
    roleField.append(roleLabel, roleSelect);
    const officeField = document.createElement("div");
    officeField.className = "field";
    const officeLabel = document.createElement("label");
    officeLabel.textContent = "Ofis (boş bırakılırsa mevcut ofis korunur)";
    const officeSelect = document.createElement("select");
    officeSelect.setAttribute("aria-label", `${user.fullName} için ofis`);
    officeSelect.replaceChildren(option("", "Mevcut ofisi koru"),
      ...state.offices.map((office) => option(office.id, office.name)));
    officeSelect.value = user.officeId == null ? "" : String(user.officeId);
    officeField.append(officeLabel, officeSelect);
    const submit = document.createElement("button");
    submit.type = "submit";
    submit.className = "secondary-button";
    submit.textContent = "Rolü kaydet";
    form.append(roleField, officeField, submit);
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      if (roleSelect.value === "OfficeManager" && !officeSelect.value) {
        showNotice("Ofis Yöneticisi için bir ofis seçin.");
        return;
      }
      submit.disabled = true;
      showNotice("");
      try {
        await apiRequest(`/api/users/${user.id}/role`, {
          method: "PUT", body: {
            role: roleSelect.value,
            officeId: officeSelect.value ? Number(officeSelect.value) : null
          }
        });
        await loadUsers();
        showNotice(`${user.fullName} kullanıcısının rolü güncellendi.`, "success");
      } catch (error) { handleError(error); submit.disabled = false; }
    });
    card.append(form);
    return card;
  });
  $("#users-list").replaceChildren(...cards);
  $("#users-count").textContent = `${result.totalCount} kullanıcı`;
  const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
  $("#users-page").textContent = `${result.page} / ${pages}`;
  $("#users-pagination").hidden = result.totalCount <= pageSize;
  $("#users-previous").disabled = result.page <= 1;
  $("#users-next").disabled = result.page >= pages;
}

async function loadUsers() {
  const result = await apiRequest(`/api/users?page=${state.usersPage}&pageSize=${pageSize}`);
  if (state.usersPage > 1 && result.items.length === 0) {
    state.usersPage -= 1;
    return loadUsers();
  }
  renderUsers(result);
}

function resetRoomForm() {
  state.roomEdit = null;
  roomForm.reset();
  roomOffice.disabled = state.user.role !== "Admin";
  roomOffice.value = state.user.role === "Admin" ? "" : String(state.user.officeId);
  $("#room-submit").textContent = "Oda ekle";
  $("#room-cancel").hidden = true;
}

function renderRooms(result) {
  const cards = result.items.map((room) => {
    const card = document.createElement("article");
    card.className = "management-card";
    const heading = document.createElement("h3");
    heading.textContent = room.name;
    const info = document.createElement("p");
    const office = state.offices.find((item) => item.id === room.officeId);
    info.textContent = `${office?.name || `Ofis ${room.officeId}`} · ${room.capacity} kişi · ${room.floor}. kat · ${room.isActive ? "Aktif" : "Pasif"}`;
    const equipment = document.createElement("p");
    equipment.textContent = `Ekipman: ${room.equipment.map((item) => item.name).join(", ") || "Yok"}`;
    const actions = document.createElement("div");
    actions.className = "management-actions";
    actions.append(
      action("Düzenle", () => {
        state.roomEdit = room.id;
        roomOffice.value = String(room.officeId);
        roomOffice.disabled = true;
        $("#room-name").value = room.name;
        $("#room-capacity").value = String(room.capacity);
        $("#room-floor").value = String(room.floor);
        $("#room-active").checked = room.isActive;
        const equipmentIds = new Set(room.equipment.map((item) => item.id));
        for (const input of $("#room-equipment").querySelectorAll('input[type="checkbox"]')) {
          input.checked = equipmentIds.has(Number(input.value));
        }
        $("#room-submit").textContent = "Değişiklikleri kaydet";
        $("#room-cancel").hidden = false;
        roomForm.scrollIntoView({ block: "center" });
      }),
      action("Sil", async () => {
        if (!window.confirm(`${room.name} odasını silmek istiyor musunuz? Rezervasyon varsa işlem reddedilir.`)) return;
        try {
          await apiRequest(`/api/rooms/${room.id}`, { method: "DELETE" });
          if (state.roomEdit === room.id) resetRoomForm();
          await loadRooms();
          showNotice("Oda silindi.", "success");
        } catch (error) { handleError(error); }
      }, true)
    );
    card.append(heading, info, equipment, actions);
    return card;
  });
  $("#managed-room-list").replaceChildren(...cards);
  $("#room-count").textContent = `${result.totalCount} oda`;
  const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
  $("#management-page").textContent = `${result.page} / ${pages}`;
  $("#management-pagination").hidden = result.totalCount <= pageSize;
  $("#management-previous").disabled = result.page <= 1;
  $("#management-next").disabled = result.page >= pages;
}

async function loadRooms() {
  const params = new URLSearchParams({ page: String(state.page), pageSize: String(pageSize) });
  const officeId = state.user.role === "Admin" ? officeFilter.value : String(state.user.officeId);
  if (officeId) params.set("officeId", officeId);
  const result = await apiRequest(`/api/rooms?${params}`);
  if (state.page > 1 && result.items.length === 0) { state.page -= 1; return loadRooms(); }
  renderRooms(result);
}

officeForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = $("#office-submit");
  button.disabled = true;
  showNotice("");
  try {
    const id = state.officeEdit;
    await apiRequest(id ? `/api/offices/${id}` : "/api/offices", {
      method: id ? "PUT" : "POST",
      body: { name: $("#office-name").value.trim(), city: $("#office-city").value.trim() }
    });
    resetOfficeForm();
    await loadOffices();
    await loadRooms();
    await loadUsers();
    showNotice(id ? "Ofis güncellendi." : "Ofis eklendi.", "success");
  } catch (error) { handleError(error); }
  finally { button.disabled = false; }
});
$("#office-cancel").addEventListener("click", resetOfficeForm);

equipmentForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = $("#equipment-submit");
  button.disabled = true;
  showNotice("");
  try {
    await apiRequest("/api/equipment", { method: "POST", body: { name: $("#equipment-name").value.trim() } });
    equipmentForm.reset();
    await loadEquipment();
    showNotice("Ekipman eklendi.", "success");
  } catch (error) { handleError(error); }
  finally { button.disabled = false; }
});

roomForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = $("#room-submit");
  button.disabled = true;
  showNotice("");
  try {
    const id = state.roomEdit;
    const selectedOfficeId = roomOffice.value;
    const details = {
      name: $("#room-name").value.trim(), capacity: Number($("#room-capacity").value),
      floor: Number($("#room-floor").value), isActive: $("#room-active").checked,
      equipmentIds: [...$("#room-equipment").querySelectorAll('input[type="checkbox"]:checked')]
        .map((input) => Number(input.value))
    };
    await apiRequest(id ? `/api/rooms/${id}` : "/api/rooms", {
      method: id ? "PUT" : "POST",
      body: id ? details : { ...details, officeId: Number(selectedOfficeId) }
    });
    resetRoomForm();
    if (!id && state.user.role === "Admin") officeFilter.value = selectedOfficeId;
    state.page = 1;
    await loadRooms();
    showNotice(id ? "Oda güncellendi." : "Oda eklendi.", "success");
  } catch (error) { handleError(error); }
  finally { button.disabled = false; }
});
$("#room-cancel").addEventListener("click", resetRoomForm);
officeFilter.addEventListener("change", async () => {
  state.page = 1;
  try { await loadRooms(); } catch (error) { handleError(error); }
});
$("#management-previous").addEventListener("click", async () => {
  state.page -= 1;
  try { await loadRooms(); } catch (error) { handleError(error); }
});
$("#management-next").addEventListener("click", async () => {
  state.page += 1;
  try { await loadRooms(); } catch (error) { handleError(error); }
});
$("#users-previous").addEventListener("click", async () => {
  state.usersPage -= 1;
  try { await loadUsers(); } catch (error) { handleError(error); }
});
$("#users-next").addEventListener("click", async () => {
  state.usersPage += 1;
  try { await loadUsers(); } catch (error) { handleError(error); }
});

$("#sign-out").addEventListener("click", async (event) => {
  const button = event.currentTarget;
  button.disabled = true;
  try {
    await apiRequest("/api/auth/logout", { method: "POST" });
    clearToken();
    location.replace("/");
  } catch (error) {
    if (error.status === 401) { clearToken(); location.replace("/"); return; }
    handleError(error);
    button.disabled = false;
  }
});

async function initialize() {
  if (!getToken()) { location.replace("/"); return; }
  try {
    state.user = await apiRequest("/api/auth/me");
    if (state.user.role !== "Admin" && state.user.role !== "OfficeManager") {
      showNotice("Bu sayfayı yalnızca Admin ve Ofis Yöneticisi kullanabilir.");
      return;
    }
    $("#user-role").textContent = state.user.role === "Admin" ? "Yönetici" : "Ofis yöneticisi";
    if (state.user.role === "OfficeManager") {
      $("#page-title").textContent = "Ofisinizin odaları";
      $("#page-description").textContent =
        "Odaları ekleyin, bilgilerini ve ekipmanlarını güncelleyin veya kullanımdan kaldırın.";
      document.title = "Oda yönetimi | Meetspace";
    }
    applyRoleMenu(state.user);
    await loadOffices();
    await loadEquipment();
    $("#office-panel").hidden = state.user.role !== "Admin";
    $("#equipment-panel").hidden = state.user.role !== "Admin";
    $("#users-panel").hidden = state.user.role !== "Admin";
    $("#room-filter-field").hidden = state.user.role !== "Admin";
    resetRoomForm();
    $("#room-panel").hidden = false;
    await loadRooms();
    if (state.user.role === "Admin") await loadUsers();
  } catch (error) { handleError(error); }
}

initialize();
