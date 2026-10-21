import { apiRequest, clearToken, getToken } from "./api.js";
import { applyRoleMenu } from "./role-menu.js";

const roomId = Number(new URLSearchParams(location.search).get("roomId"));
const board = document.querySelector("#calendar-board");
const busyList = document.querySelector("#busy-list");
const notice = document.querySelector("#calendar-notice");
const emptyMessage = document.querySelector("#calendar-empty");
const weekLabel = document.querySelector("#week-label");
const weekButtons = [...document.querySelectorAll(".calendar-buttons button")];
const dayMilliseconds = 24 * 60 * 60 * 1000;
const hourStart = 8 * 60;
const hourEnd = 20 * 60;
let weekStart;

const turkeyPartsFormatter = new Intl.DateTimeFormat("en-GB", {
  timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit",
  hour: "2-digit", minute: "2-digit", hourCycle: "h23"
});
const dayFormatter = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "UTC", weekday: "long", day: "numeric", month: "short"
});
const rangeFormatter = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "UTC", day: "numeric", month: "long", year: "numeric"
});
const busyDateFormatter = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "Europe/Istanbul", weekday: "long", day: "numeric", month: "long"
});
const busyTimeFormatter = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "Europe/Istanbul", hour: "2-digit", minute: "2-digit"
});

function showNotice(message) {
  notice.textContent = message;
  notice.hidden = !message;
}

function turkeyParts(date) {
  return Object.fromEntries(turkeyPartsFormatter.formatToParts(date).map((part) => [part.type, part.value]));
}

function mondayInTurkey() {
  const today = turkeyParts(new Date());
  const day = new Date(Date.UTC(Number(today.year), Number(today.month) - 1, Number(today.day)));
  return new Date(day.getTime() - ((day.getUTCDay() + 6) % 7) * dayMilliseconds);
}

function dateKey(date) {
  return date.toISOString().slice(0, 10);
}

function appendDay(day, slots) {
  const column = document.createElement("div");
  column.className = "calendar-day";
  const heading = document.createElement("div");
  heading.className = "calendar-day-heading";
  heading.textContent = dayFormatter.format(day);
  const track = document.createElement("div");
  track.className = "calendar-day-track";
  const key = dateKey(day);

  for (const slot of slots) {
    const startUtc = new Date(slot.startsAtUtc);
    const endUtc = new Date(slot.endsAtUtc);
    const start = turkeyParts(startUtc);
    if (`${start.year}-${start.month}-${start.day}` !== key) continue;

    const end = turkeyParts(endUtc);
    const startMinute = Number(start.hour) * 60 + Number(start.minute);
    const endMinute = `${end.year}-${end.month}-${end.day}` === key
      ? Number(end.hour) * 60 + Number(end.minute) : hourEnd;
    const top = Math.max(0, startMinute - hourStart);
    const bottom = Math.min(hourEnd - hourStart, endMinute - hourStart);
    if (bottom <= top) continue;

    const block = document.createElement("div");
    block.className = "calendar-busy";
    block.style.top = `${top}px`;
    block.style.height = `${bottom - top}px`;
    const label = `${busyTimeFormatter.format(startUtc)}–${busyTimeFormatter.format(endUtc)}`;
    block.textContent = label;
    block.title = `Dolu: ${label}`;
    block.setAttribute("aria-label", `Dolu: ${dayFormatter.format(day)}, ${label}`);
    block.tabIndex = 0;
    track.append(block);
  }

  column.append(heading, track);
  board.append(column);
}

function renderCalendar(result) {
  board.replaceChildren();
  busyList.replaceChildren();
  const endOfWeek = new Date(weekStart.getTime() + 6 * dayMilliseconds);
  weekLabel.textContent = `${rangeFormatter.format(weekStart)} – ${rangeFormatter.format(endOfWeek)}`;

  const rail = document.createElement("div");
  rail.className = "calendar-time-column";
  const heading = document.createElement("div");
  heading.className = "calendar-day-heading";
  heading.textContent = "Saat";
  const times = document.createElement("div");
  times.className = "calendar-time-track";
  for (let hour = 8; hour <= 20; hour += 1) {
    const label = document.createElement("span");
    label.style.top = `${(hour - 8) * 60}px`;
    label.textContent = `${String(hour).padStart(2, "0")}:00`;
    times.append(label);
  }
  rail.append(heading, times);
  board.append(rail);

  for (let index = 0; index < 7; index += 1) {
    appendDay(new Date(weekStart.getTime() + index * dayMilliseconds), result.busySlots);
  }
  for (const slot of result.busySlots) {
    const item = document.createElement("li");
    item.textContent = `${busyDateFormatter.format(new Date(slot.startsAtUtc))}: ${busyTimeFormatter.format(new Date(slot.startsAtUtc))}–${busyTimeFormatter.format(new Date(slot.endsAtUtc))}`;
    busyList.append(item);
  }
  emptyMessage.hidden = result.busySlots.length !== 0;
}

async function loadCalendar() {
  showNotice("");
  board.replaceChildren();
  busyList.replaceChildren();
  emptyMessage.hidden = true;
  board.setAttribute("aria-busy", "true");
  weekButtons.forEach((button) => { button.disabled = true; });
  weekLabel.textContent = "Takvim yükleniyor...";
  try {
    const query = new URLSearchParams({ weekStart: dateKey(weekStart) });
    const result = await apiRequest(`/api/rooms/${roomId}/calendar?${query}`);
    renderCalendar(result);
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    weekLabel.textContent = "";
    showNotice(error.message || "Takvim yüklenemedi.");
  } finally {
    board.setAttribute("aria-busy", "false");
    weekButtons.forEach((button) => { button.disabled = false; });
  }
}

document.querySelector("#previous-week").addEventListener("click", () => {
  weekStart = new Date(weekStart.getTime() - 7 * dayMilliseconds);
  loadCalendar();
});
document.querySelector("#next-week").addEventListener("click", () => {
  weekStart = new Date(weekStart.getTime() + 7 * dayMilliseconds);
  loadCalendar();
});
document.querySelector("#current-week").addEventListener("click", () => {
  weekStart = mondayInTurkey();
  loadCalendar();
});
document.querySelector("#refresh-calendar").addEventListener("click", loadCalendar);

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
    showNotice("Takvimi görmek için oda listesinden bir oda seçin.");
    return;
  }
  weekStart = mondayInTurkey();
  try {
    const user = await apiRequest("/api/auth/me");
    const roleNames = { Admin: "Yönetici", OfficeManager: "Ofis yöneticisi", Employee: "Çalışan" };
    document.querySelector("#user-role").textContent = roleNames[user.role] || user.role;
    applyRoleMenu(user);
    const room = await apiRequest(`/api/rooms/${roomId}`);
    document.querySelector("#page-title").textContent = `${room.name} takvimi`;
    document.querySelector("#room-details").textContent = `${room.capacity} kişi · ${room.floor}. kat`;
    if (room.isActive) {
      const book = document.querySelector("#calendar-book");
      book.href = `/reserve.html?${new URLSearchParams({ roomId: String(room.id) })}`;
      book.hidden = false;
    }
    await loadCalendar();
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    showNotice(error.message || "Oda takvimi yüklenemedi.");
  }
}

initialize();
