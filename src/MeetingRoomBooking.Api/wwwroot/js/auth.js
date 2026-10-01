import { apiRequest, clearToken, getToken, saveToken } from "./api.js";

const guestView = document.querySelector("#guest-view");
const accountView = document.querySelector("#account-view");
const loginForm = document.querySelector("#login-form");
const registerForm = document.querySelector("#register-form");
const loginTab = document.querySelector("#login-tab");
const registerTab = document.querySelector("#register-tab");
const notice = document.querySelector("#notice");

const roleNames = {
  Admin: "Yönetici",
  OfficeManager: "Ofis yöneticisi",
  Employee: "Çalışan"
};

function showNotice(message, kind = "error") {
  notice.textContent = message;
  notice.dataset.kind = kind;
  notice.hidden = !message;
}

function selectTab(tab) {
  const loginSelected = tab === "login";
  loginForm.hidden = !loginSelected;
  registerForm.hidden = loginSelected;
  loginTab.classList.toggle("active", loginSelected);
  registerTab.classList.toggle("active", !loginSelected);
  loginTab.setAttribute("aria-selected", String(loginSelected));
  registerTab.setAttribute("aria-selected", String(!loginSelected));
  showNotice("");
}

function showGuest() {
  guestView.hidden = false;
  accountView.hidden = true;
}

function showAccount(user) {
  document.querySelector("#welcome-name").textContent = `Merhaba, ${user.fullName}`;
  document.querySelector("#profile-role").textContent = roleNames[user.role] || user.role;
  document.querySelector("#profile-email").textContent = user.email;
  guestView.hidden = true;
  accountView.hidden = false;
}

async function submitForm(form, action) {
  const button = form.querySelector('button[type="submit"]');
  button.disabled = true;
  showNotice("");
  try {
    await action();
  } catch (error) {
    showNotice(error.message || "İşlem tamamlanamadı.");
  } finally {
    button.disabled = false;
  }
}

loginTab.addEventListener("click", () => selectTab("login"));
registerTab.addEventListener("click", () => selectTab("register"));

loginForm.addEventListener("submit", (event) => {
  event.preventDefault();
  submitForm(loginForm, async () => {
    const fields = new FormData(loginForm);
    const result = await apiRequest("/api/auth/login", {
      method: "POST", authenticated: false,
      body: { email: fields.get("email"), password: fields.get("password") }
    });
    saveToken(result.token);
    loginForm.reset();
    showAccount(result.user);
  });
});

registerForm.addEventListener("submit", (event) => {
  event.preventDefault();
  submitForm(registerForm, async () => {
    const fields = new FormData(registerForm);
    const password = fields.get("password");
    if (password !== fields.get("passwordRepeat")) {
      throw new Error("Şifreler aynı olmalıdır.");
    }

    const result = await apiRequest("/api/auth/register", {
      method: "POST", authenticated: false,
      body: { fullName: fields.get("fullName"), email: fields.get("email"), password }
    });
    saveToken(result.token);
    registerForm.reset();
    showAccount(result.user);
    showNotice("Hesabınız oluşturuldu.", "success");
  });
});

document.querySelector("#logout-button").addEventListener("click", async (event) => {
  const button = event.currentTarget;
  button.disabled = true;
  showNotice("");
  try {
    await apiRequest("/api/auth/logout", { method: "POST" });
    clearToken();
    showGuest();
    showNotice("Çıkış yaptınız.", "success");
  } catch (error) {
    if (error.status === 401) {
      clearToken();
      showGuest();
      showNotice("Oturumunuz sona erdi. Yeniden giriş yapabilirsiniz.");
    } else {
      showNotice(error.message || "Çıkış tamamlanamadı. Yeniden deneyin.");
    }
  } finally {
    button.disabled = false;
  }
});

if (getToken()) {
  apiRequest("/api/auth/me")
    .then(showAccount)
    .catch((error) => {
      if (error.status === 401) clearToken();
      showGuest();
      showNotice(error.message || "Oturum doğrulanamadı.");
    });
} else {
  showGuest();
}
