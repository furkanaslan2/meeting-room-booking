import { apiRequest, clearToken, getToken } from "./api.js";

const params = new URLSearchParams(location.search);
const roomId = Number(params.get("roomId"));
const isEditing = params.has("reservationId");
const reservationId = Number(params.get("reservationId"));
const notice = document.querySelector("#booking-notice");
const panel = document.querySelector("#booking-panel");
const form = document.querySelector("#booking-form");
const titleInput = document.querySelector("#booking-title-input");
const startInput = document.querySelector("#booking-start");
const endInput = document.querySelector("#booking-end");
const participantList = document.querySelector("#participant-list");
const addParticipantButton = document.querySelector("#add-participant");
const bookButton = document.querySelector("#book-button");
let room;

function showNotice(message) {
  notice.textContent = message;
  notice.hidden = !message;
}

function turkeyDate(date) {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit"
  }).formatToParts(date);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}`;
}

function localDateTime(value) {
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) ? value : "";
}

function turkeyDisplay(value) {
  return new Intl.DateTimeFormat("tr-TR", {
    timeZone: "Europe/Istanbul", year: "numeric", month: "long", day: "numeric",
    hour: "2-digit", minute: "2-digit"
  }).format(new Date(value));
}

function turkeyInput(value) {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", hourCycle: "h23"
  }).formatToParts(new Date(value));
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}T${values.hour}:${values.minute}`;
}

function updateCapacityHint() {
  const guests = participantList.children.length;
  document.querySelector("#capacity-hint").textContent =
    `${guests + 1} / ${room.capacity} kişi (siz dahil)`;
  addParticipantButton.disabled = guests >= room.capacity - 1;
}

function addParticipant(name = "", email = "", existing = false) {
  if (!existing && participantList.children.length >= room.capacity - 1) return;
  const row = document.createElement("div");
  row.className = "participant-row";

  const nameField = document.createElement("label");
  nameField.textContent = "Ad soyad";
  const nameInput = document.createElement("input");
  nameInput.type = "text";
  nameInput.maxLength = 150;
  nameInput.required = true;
  nameInput.value = name;
  nameField.append(nameInput);

  const emailField = document.createElement("label");
  emailField.textContent = "E-posta (isteğe bağlı)";
  const emailInput = document.createElement("input");
  emailInput.type = "email";
  emailInput.maxLength = 255;
  emailInput.value = email;
  emailField.append(emailInput);

  const remove = document.createElement("button");
  remove.className = "secondary-button";
  remove.type = "button";
  remove.textContent = "Kaldır";
  remove.setAttribute("aria-label", "Katılımcıyı kaldır");
  remove.addEventListener("click", () => { row.remove(); updateCapacityHint(); });
  row.append(nameField, emailField, remove);
  participantList.append(row);
  updateCapacityHint();
  if (!existing) nameInput.focus();
}

function validateTimes(start, end) {
  if (!start || !end) return "Başlangıç ve bitiş saatlerini girin.";
  const startUtc = Date.parse(`${start}:00+03:00`);
  const endUtc = Date.parse(`${end}:00+03:00`);
  if (Number.isNaN(startUtc) || Number.isNaN(endUtc)) return "Geçerli bir tarih ve saat seçin.";
  if (startUtc <= Date.now()) return "Geçmiş zamana rezervasyon yapılamaz.";
  const minutes = (endUtc - startUtc) / 60000;
  if (minutes < 15 || minutes > 240) return "Rezervasyon süresi 15 dakika ile 4 saat arasında olmalıdır.";
  if (start.slice(0, 10) !== end.slice(0, 10) || start.slice(11) < "08:00" || end.slice(11) > "20:00") {
    return "Rezervasyon Türkiye saatiyle aynı gün 08:00–20:00 arasında olmalıdır.";
  }
  return "";
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  showNotice("");
  const start = startInput.value;
  const end = endInput.value;
  const timeError = validateTimes(start, end);
  if (timeError) { showNotice(timeError); return; }

  const title = titleInput.value.trim();
  if (!title) { showNotice("Toplantı başlığını girin."); return; }
  const participants = [...participantList.children].map((row) => ({
    name: row.querySelector('input[type="text"]').value.trim(),
    email: row.querySelector('input[type="email"]').value.trim() || null
  }));
  if (participants.some((person) => !person.name)) {
    showNotice("Her katılımcının adını girin.");
    return;
  }
  if (participants.length + 1 > room.capacity) { showNotice("Odanın kapasitesi aşılamaz."); return; }

  bookButton.disabled = true;
  bookButton.textContent = "Kaydediliyor...";
  try {
    const result = await apiRequest(isEditing ? `/api/reservations/${reservationId}` : "/api/reservations", {
      method: isEditing ? "PUT" : "POST", body: {
        roomId: room.id, title, startsAt: `${start}:00+03:00`, endsAt: `${end}:00+03:00`, participants
      }
    });
    panel.hidden = true;
    document.querySelector("#booking-summary").textContent =
      `${room.name} · ${turkeyDisplay(result.startsAtUtc)} – ${turkeyDisplay(result.endsAtUtc)} · Rezervasyon #${result.id}`;
    document.querySelector("#booking-success").hidden = false;
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    showNotice(error.message || "Rezervasyon oluşturulamadı.");
    bookButton.disabled = false;
    bookButton.textContent = isEditing ? "Değişiklikleri kaydet →" : "Rezervasyonu oluştur →";
  }
});

addParticipantButton.addEventListener("click", () => addParticipant());

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
  if (!Number.isSafeInteger(roomId) || roomId <= 0) {
    showNotice("Rezervasyon oluşturmak için önce oda listesinden bir oda seçin.");
    return;
  }
  if (isEditing && (!Number.isSafeInteger(reservationId) || reservationId <= 0)) {
    showNotice("Geçersiz rezervasyon numarası.");
    return;
  }

  const tomorrow = turkeyDate(new Date(Date.now() + 24 * 60 * 60 * 1000));
  startInput.value = localDateTime(params.get("startsAt")) || `${tomorrow}T10:00`;
  endInput.value = localDateTime(params.get("endsAt")) || `${tomorrow}T11:00`;

  try {
    const user = await apiRequest("/api/auth/me");
    const roleNames = { Admin: "Yönetici", OfficeManager: "Ofis yöneticisi", Employee: "Çalışan" };
    document.querySelector("#user-role").textContent = roleNames[user.role] || user.role;
    room = await apiRequest(`/api/rooms/${roomId}`);
    if (!room.isActive) { showNotice("Bu oda pasif olduğu için rezerve edilemez."); return; }
    document.querySelector("#selected-room").textContent =
      `${room.name} · ${room.capacity} kişi · ${room.floor}. kat`;
    updateCapacityHint();
    if (isEditing) {
      const reservation = await apiRequest(`/api/reservations/${reservationId}`);
      if (reservation.roomId !== room.id || reservation.status !== "Active" ||
          Date.parse(reservation.startsAtUtc) <= Date.now()) {
        showNotice("Bu rezervasyon artık düzenlenemiyor. Listeyi yenileyip tekrar deneyin.");
        return;
      }
      document.querySelector("#booking-eyebrow").textContent = "REZERVASYONU DÜZENLE";
      document.querySelector("#page-title").textContent = "Toplantınızı güncelleyin.";
      document.querySelector("#booking-title").textContent = "Rezervasyon bilgilerini düzenle";
      document.querySelector("#success-title").textContent = "Değişiklikler kaydedildi.";
      titleInput.value = reservation.title;
      startInput.value = turkeyInput(reservation.startsAtUtc);
      endInput.value = turkeyInput(reservation.endsAtUtc);
      for (const participant of reservation.participants) {
        addParticipant(participant.name, participant.email || "", true);
      }
      bookButton.textContent = "Değişiklikleri kaydet →";
    }
    panel.hidden = false;
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    showNotice(error.message || "Oda bilgileri yüklenemedi.");
  }
}

initialize();
