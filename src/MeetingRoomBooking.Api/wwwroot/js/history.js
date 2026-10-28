import { apiRequest, clearToken, getToken } from "./api.js";
import { applyRoleMenu } from "./role-menu.js";

const $ = (selector) => document.querySelector(selector);
const params = new URLSearchParams(location.search);
const reservationId = Number(params.get("reservationId"));
const pageSize = 10;
let page = 1;

function showNotice(message) {
  $("#history-notice").textContent = message;
  $("#history-notice").hidden = !message;
}

function turkeyDateTime(value) {
  return new Intl.DateTimeFormat("tr-TR", {
    timeZone: "Europe/Istanbul", year: "numeric", month: "long", day: "numeric",
    hour: "2-digit", minute: "2-digit"
  }).format(new Date(value));
}

function snapshotDetails(snapshot) {
  if (!snapshot) return "Önceki kayıt yok.";
  return `Oda #${snapshot.roomId} · ${snapshot.title} · ${turkeyDateTime(snapshot.startsAtUtc)} – ${turkeyDateTime(snapshot.endsAtUtc)} · ${snapshot.status === "Active" ? "Aktif" : "İptal edildi"} · ${snapshot.participantCount} katılımcı`;
}

function renderEntry(entry) {
  const labels = { Created: "Oluşturuldu", Updated: "Düzenlendi", Cancelled: "İptal edildi" };
  const card = document.createElement("article");
  card.className = "reservation-card";
  const heading = document.createElement("h3");
  heading.textContent = labels[entry.action] || entry.action;
  const actor = document.createElement("p");
  actor.className = "reservation-date";
  actor.textContent = `${entry.actorFullName} · ${turkeyDateTime(entry.occurredAtUtc)} (Türkiye saati)`;
  const beforeTitle = document.createElement("strong");
  beforeTitle.textContent = "Önce";
  const before = document.createElement("p");
  before.className = "reservation-guests";
  before.textContent = snapshotDetails(entry.before);
  const afterTitle = document.createElement("strong");
  afterTitle.textContent = "Sonra";
  const after = document.createElement("p");
  after.className = "reservation-guests";
  after.textContent = snapshotDetails(entry.after);
  card.append(heading, actor, beforeTitle, before, afterTitle, after);
  return card;
}

async function loadHistory() {
  showNotice("");
  const button = $("#history-refresh");
  button.disabled = true;
  try {
    const result = await apiRequest(`/api/reservations/${reservationId}/history?page=${page}&pageSize=${pageSize}`);
    $("#history-list").replaceChildren(...result.items.map(renderEntry));
    $("#history-empty").hidden = result.totalCount !== 0;
    $("#history-count").textContent = `${result.totalCount} işlem`;
    const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
    $("#history-pagination").hidden = result.totalCount <= pageSize;
    $("#history-page").textContent = `${result.page} / ${pages}`;
    $("#history-previous").disabled = result.page <= 1;
    $("#history-next").disabled = result.page >= pages;
  } catch (error) {
    if (error.status === 401) { clearToken(); location.replace("/"); return; }
    showNotice(error.message || "Geçmiş yüklenemedi.");
  } finally { button.disabled = false; }
}

$("#history-refresh").addEventListener("click", loadHistory);
$("#history-previous").addEventListener("click", () => { page -= 1; loadHistory(); });
$("#history-next").addEventListener("click", () => { page += 1; loadHistory(); });
$("#sign-out").addEventListener("click", async (event) => {
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
  if (!Number.isSafeInteger(reservationId) || reservationId <= 0) {
    showNotice("Geçerli bir rezervasyon seçin.");
    return;
  }
  if (params.get("view") === "team") $("#history-back").href = "/reservations.html";
  try {
    const user = await apiRequest("/api/auth/me");
    const roleNames = { Admin: "Yönetici", OfficeManager: "Ofis yöneticisi", Employee: "Çalışan" };
    $("#user-role").textContent = roleNames[user.role] || user.role;
    applyRoleMenu(user);
    const reservation = await apiRequest(`/api/reservations/${reservationId}`);
    $("#history-reservation").textContent = `${reservation.title} · Rezervasyon #${reservation.id}`;
    $("#history-panel").hidden = false;
    await loadHistory();
  } catch (error) {
    if (error.status === 401) { clearToken(); location.replace("/"); return; }
    showNotice(error.message || "Rezervasyon yüklenemedi.");
  }
}

initialize();
