export function applyRoleMenu(user) {
  if (user.role !== "Admin" && user.role !== "OfficeManager") return;
  const nav = document.querySelector(".app-nav");
  if (!nav || nav.querySelector("[data-report-link]")) return;

  const link = document.createElement("a");
  link.href = "/reports.html";
  link.textContent = "Raporlar";
  link.dataset.reportLink = "true";
  nav.insertBefore(link, nav.querySelector('a[href="/"]'));
}
