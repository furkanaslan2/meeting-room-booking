export function applyRoleMenu(user) {
  if (user.role !== "Admin" && user.role !== "OfficeManager") return;
  const nav = document.querySelector(".app-nav");
  if (!nav) return;
  const teamLabel = user.role === "Admin" ? "Tüm rezervasyonlar" : "Ofis rezervasyonları";
  const existingTeam = nav.querySelector("[data-team-link]");
  if (existingTeam) existingTeam.textContent = teamLabel;

  if (!existingTeam) {
    const team = document.createElement("a");
    team.href = "/reservations.html";
    team.textContent = teamLabel;
    team.dataset.teamLink = "true";
    nav.insertBefore(team, nav.querySelector('a[href="/"]'));
  }
  if (!nav.querySelector("[data-report-link]")) {
    const report = document.createElement("a");
    report.href = "/reports.html";
    report.textContent = "Raporlar";
    report.dataset.reportLink = "true";
    nav.insertBefore(report, nav.querySelector('a[href="/"]'));
  }
  if (!nav.querySelector("[data-management-link]")) {
    const management = document.createElement("a");
    management.href = "/manage.html";
    management.textContent = "Yönetim";
    management.dataset.managementLink = "true";
    nav.insertBefore(management, nav.querySelector('a[href="/"]'));
  }
}
