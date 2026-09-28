# Meeting Room Booking

Staj projesi: toplantı odası rezervasyon sistemi. Şu anda proje iskeleti ve veritabanı modeli hazırdır. API özellikleri sonraki aşamalarda eklenecektir.

## Gerekenler

- .NET 8 SDK veya üzeri
- Git
- MySQL 8 veya üzeri (veritabanı aşamasında kullanılacak)

## Veritabanı modelini kurma (Windows PowerShell)

1. `dotnet --version` komutuyla SDK'nın kurulu olduğunu kontrol et.
2. MySQL 8 sunucusunun çalıştığından emin ol.
3. `Copy-Item src/MeetingRoomBooking.Api/appsettings.Example.json src/MeetingRoomBooking.Api/appsettings.json` çalıştır. Oluşan `appsettings.json` içindeki `YOUR_DB_USER` ve `YOUR_DB_PASSWORD` alanlarını kendi yerel MySQL bilgilerinle değiştir. Bu dosyayı Git'e ekleme.
4. Repo kökünde `dotnet restore MeetingRoomBooking.sln` ve `dotnet build MeetingRoomBooking.sln` çalıştır.
5. `dotnet tool restore` çalıştır.
6. `dotnet ef migrations add InitialCreate --project src/MeetingRoomBooking.Data --startup-project src/MeetingRoomBooking.Api --output-dir Migrations` çalıştır. Oluşan migration dosyaları Git'e eklenmelidir.
7. `dotnet ef database update --project src/MeetingRoomBooking.Data --startup-project src/MeetingRoomBooking.Api` çalıştır.
8. `dotnet run --project src/MeetingRoomBooking.Api --no-launch-profile -- --urls http://localhost:5080` çalıştır.
9. Tarayıcıda `http://localhost:5080/swagger` ve `http://localhost:5080/api/status` adreslerini aç.

`src/MeetingRoomBooking.Api/appsettings.Example.json` yalnızca ayarların biçimini gösterir. Gerçek bağlantı ve JWT sırları Git'e yüklenmez. Migration dosyaları EF Core tarafından oluşturulur; içlerinde şifre olmamalıdır.

Bu aşamada ilk çalıştırma örnek verileri, kimlik doğrulama, rezervasyon ve arayüz henüz uygulanmadı.
