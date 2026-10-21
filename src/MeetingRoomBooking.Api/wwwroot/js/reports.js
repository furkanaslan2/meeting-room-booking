import { apiRequest, clearToken, getToken } from "./api.js";

const notice = document.querySelector("#report-notice");
const panel = document.querySelector("#report-panel");
const officeField = document.querySelector("#office-field");
const officeFilter = document.querySelector("#office-filter");
const refreshButton = document.querySelector("#refresh-report");
const rows = document.querySelector("#report-rows");
const tableWrap = document.querySelector("#report-table-wrap");
const emptyMessage = document.querySelector("#report-empty");
const pagination = document.querySelector("#pagination");
const previousPage = document.querySelector("#previous-page");
const nextPage = document.querySelector("#next-page");
const officeNames = new Map();
const pageSize = 10;
let page = 1;

const dateFormatter = new Intl.DateTimeFormat("tr-TR", {
  timeZone: "Europe/Istanbul", year: "numeric", month: "long", day: "numeric"
});

function showNotice(message) {
  notice.textContent = message;
  notice.hidden = !message;
}

function addCell(row, value) {
  const cell = document.createElement("td");
  cell.textContent = value;
  row.append(cell);
  return cell;
}

function renderReport(result) {
  const rendered = result.items.map((item) => {
    const row = document.createElement("tr");
    addCell(row, item.roomName);
    addCell(row, officeNames.get(item.officeId) || `Ofis ${item.officeId}`);
    addCell(row, String(item.reservationCount));
    addCell(row, `${item.occupiedMinutes.toLocaleString("tr-TR")} dk`);
    const percent = Number(item.occupancyPercent);
    const cell = addCell(row, `${percent.toLocaleString("tr-TR")} %`);
    const meter = document.createElement("span");
    meter.className = "usage-meter";
    meter.setAttribute("aria-hidden", "true");
    const fill = document.createElement("span");
    fill.style.width = `${Math.max(0, Math.min(100, percent))}%`;
    meter.append(fill);
    cell.append(meter);
    return row;
  });
  rows.replaceChildren(...rendered);
  tableWrap.hidden = result.items.length === 0;
  emptyMessage.hidden = result.items.length !== 0;
  document.querySelector("#report-count").textContent = `${result.totalCount} oda`;
  document.querySelector("#report-period").textContent =
    `${dateFormatter.format(new Date(result.periodStartUtc))} – ${dateFormatter.format(new Date(result.periodEndUtc))}`;
  const pages = Math.max(1, Math.ceil(result.totalCount / pageSize));
  document.querySelector("#page-label").textContent = `${result.page} / ${pages}`;
  pagination.hidden = result.totalCount <= pageSize;
  previousPage.disabled = result.page <= 1;
  nextPage.disabled = result.page >= pages;
}

async function loadReport() {
  showNotice("");
  rows.replaceChildren();
  emptyMessage.hidden = true;
  pagination.hidden = true;
  previousPage.disabled = true;
  nextPage.disabled = true;
  refreshButton.disabled = true;
  document.querySelector("#report-count").textContent = "Yükleniyor...";
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (officeFilter.value) params.set("officeId", officeFilter.value);
  try {
    const result = await apiRequest(`/api/reports/rooms?${params}`);
    renderReport(result);
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    document.querySelector("#report-count").textContent = "";
    showNotice(error.message || "Rapor yüklenemedi.");
  } finally {
    refreshButton.disabled = false;
  }
}

async function loadOffices() {
  let officePage = 1;
  while (true) {
    const result = await apiRequest(`/api/offices?page=${officePage}&pageSize=100`);
    for (const office of result.items) {
      officeNames.set(office.id, office.name);
      const option = document.createElement("option");
      option.value = String(office.id);
      option.textContent = office.name;
      officeFilter.append(option);
    }
    if (officePage * 100 >= result.totalCount) break;
    officePage += 1;
  }
}

officeFilter.addEventListener("change", () => { page = 1; loadReport(); });
refreshButton.addEventListener("click", loadReport);
previousPage.addEventListener("click", () => { page -= 1; loadReport(); });
nextPage.addEventListener("click", () => { page += 1; loadReport(); });

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
    if (user.role !== "Admin" && user.role !== "OfficeManager") {
      showNotice("Raporları yalnızca Admin ve Ofis Yöneticisi görebilir.");
      return;
    }
    if (user.role === "Admin") {
      await loadOffices();
      officeField.hidden = false;
    } else if (user.officeId) {
      const office = await apiRequest(`/api/offices/${user.officeId}`);
      officeNames.set(office.id, office.name);
    }
    panel.hidden = false;
    await loadReport();
  } catch (error) {
    if (error.status === 401) { location.replace("/"); return; }
    showNotice(error.message || "Rapor sayfası yüklenemedi.");
  }
}

initialize();
