const TOKEN_KEY = "meeting-room-booking-token";

export class ApiError extends Error {
  constructor(message, status, code) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

export function getToken() {
  return sessionStorage.getItem(TOKEN_KEY);
}

export function saveToken(token) {
  sessionStorage.setItem(TOKEN_KEY, token);
}

export function clearToken() {
  sessionStorage.removeItem(TOKEN_KEY);
}

export async function apiRequest(path, { method = "GET", body, authenticated = true } = {}) {
  const token = getToken();
  if (authenticated && !token) {
    throw new ApiError("Devam etmek için giriş yapın.", 401, "UNAUTHORIZED");
  }

  const headers = { Accept: "application/json" };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (authenticated) headers.Authorization = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store"
    });
  } catch {
    throw new ApiError("Sunucuya bağlanılamadı. Bağlantınızı kontrol edip yeniden deneyin.", 0, "NETWORK_ERROR");
  }

  const contentType = response.headers.get("content-type") || "";
  const data = contentType.includes("application/json") ? await response.json() : null;
  if (!response.ok) {
    if (authenticated && response.status === 401) clearToken();
    throw new ApiError(data?.message || "İşlem tamamlanamadı. Yeniden deneyin.",
      response.status, data?.code || "REQUEST_FAILED");
  }

  return data;
}
