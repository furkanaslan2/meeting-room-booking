import { apiRequest, clearToken, getToken } from "./api.js";

const notice = document.querySelector("#mine-notice");
const list = document.querySelector("#reservation-list");
const emptyMessage = document.querySelector("#empty-message");
const pagination = document.querySelector("#pagination");
const previousPage = document.querySelector("#previous-page");
const nextPage = document.querySelector("#next-page");
const roomNames = new Map();
const pageSize = 6;
let page = 1;

const dateFormat = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "Europe/Istanbul", year: "numeric", month: "long", day: "numeric",
  hour: "2-digit", minute: "2-digit"
});
const timeFormat = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "Europe/Istanbul", hour: "2-digit", minute: "2-digit"
});

function showNotice(message, kind = "error") {
  notice.textContent = message;
  notice.dataset.kind = kind;
  notice.hidden = !message;
}

async function getRoomName(id) {
  if (!roomNames.has(id)) {
    roomNames.set(id, apiRequest(`/api/rooms/${id}`).then((room) => room.name));
  }
  try {
    return await roomNames.get(id);
  } catch (error) {
    roomNames.delete(id);
    if (error.status === 401) throw error;
    return `Oda #${id}`;
  }
}

function renderReservation(reservation, roomName) {
  const card = document.createElement("article");
  card.className = "reservation-card";

  const top = document.createElement("div");
  top.className = "room-card-top";
  const room = document.createElement("span");
  room.className = "room-office";
  room.textContent = roomName;
  const status = document.createElement("span");
  status.className = reservation.status === "Active" ? "room-status" : "room-status inactive";
  status.textContent = reservation.status === "Active" ? "Aktif" : "İptal edildi";
  top.append(room, status);

  const heading = document.createElement("h3");
  heading.textContent = reservation.title;
  const date = document.createElement("p");
  date.className = "reservation-date";
  date.textContent = `${dateFormat.format(new Date(reservation.startsAtUtc))} – ${timeFormat.format(new Date(reservation.endsAtUtc))} (Türkiye saati)`;
  const guests = document.createElement("p");
  guests.className = "reservation-guests";
  guests.textContent = reservation.participants.length
    ? `Katılımcılar: ${reservation.participants.map((person) => person.name).join(", ")}`
    : "Katılımcı eklenmedi";
  card.append(top, heading, date, guests);

  if (reservation.status === "Active" && Date.parse(reservation.startsAtUtc) > Date.now()) {
    const actions = document.createElement("div");
    actions.className = "reservation-actions";
    const edit = document.createElement("a");
    edit.className = "secondary-button";
    const params = new URLSearchParams({
      roomId: String(reservation.roomId), reservationId: String(reservation.id)
    });
    edit.href = `/reserve.html?${params}`;
    edit.textContent = "Düzenle";

    const cancel = document.createElement("button");
    cancel.className = "secondary-button cancel-button";
    cancel.type = "button";
    cancel.textContent = "İptal et";
    cancel.addEventListener("click", async () => {
      if (!window.confirm(`“${reservation.title}” rezervasyonunu iptal etmek istiyor musunuz?`)) return;
      cancel.disabled = true;
      showNotice("");
      try {
        await apiRequest(`/api/reservations/${reservation.id}`, { method: "DELETE" });
        if (await loadReservations(true)) showNotice("Rezervasyon iptal edildi.", "success");
      } catch (error) {
        if (error.status === 401) { location.replace("/"); return; }
        showNotice(error.message || "Rezervasyon iptal edilemedi.");
        cancel.disabled = false;
      }
    });
    actions.append(edit, cancel);
    card.append(actions);
  }

  return card;
}

async function loadReservations(preserveNotice = false) {
  if (!preserveNotice) showNotice("");
  list.replaceChildren();
  list.setAttribute("aria-busy", "true");
  emptyMessage.hidden = true;
  pagination.hidden = true;
  previousPage.disabled = true;
  nextPage.disabled = true;
  document.querySelector("#reservation-count").textContent = "Yükleniyor...";
  try {
    const result = await apiRequest(`/api/reservations/mine?page=${page}&pageSize=${pageSize}`);
    const names = await Promise.all(result.items.map((item) => getRoomName(item.roomId)));
    list.replaceChildren(...result.items.map((item, index) => renderReservation(item, names[index])));
    emptyMessage.hidden = result.totalCount !== 0;
    const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
    document.querySelector("#reservation-count").textContent = `${result.totalCount} rezervasyon`;
    document.querySelector("#page-label").textContent = `${result.page} / ${pages}`;
    pagination.hidden = result.totalCount <= pageSize;
    previousPage.disabled = result.page <= 1;
    nextPage.disabled = result.page >= pages;
    return true;
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    document.querySelector("#reservation-count").textContent = "";
    showNotice(error.message || "Rezervasyonlar yüklenemedi.");
    return false;
  } finally {
    list.setAttribute("aria-busy", "false");
  }
}

previousPage.addEventListener("click", () => { page -= 1; loadReservations(); });
nextPage.addEventListener("click", () => { page += 1; loadReservations(); });

document.querySelector("#sign-out").addEventListener("click", async (event) => {
  const button = event.currentTarget;
  button.disabled = true;
  try {
    await apiRequest("/api/auth/logout", { method: "POST" });
    clearToken();
    location.replace("/");
  } catch (error) {
    if (error.status === 401) { clearToken(); location.replace("/"); return; }
    showNotice(error.message || "Çıkış tamamlanamadı.");
    button.disabled = false;
  }
});

async function initialize() {
  if (!getToken()) { location.replace("/"); return; }
  try {
    const user = await apiRequest("/api/auth/me");
    const roleNames = { Admin: "Yönetici", OfficeManager: "Ofis yöneticisi", Employee: "Çalışan" };
    document.querySelector("#user-role").textContent = roleNames[user.role] || user.role;
    await loadReservations();
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    showNotice(error.message || "Sayfa yüklenemedi.");
  }
}

initialize();
