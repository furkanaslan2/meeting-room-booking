# Meeting Room Booking

Staj projesi: toplantı odası rezervasyon sistemi. Bu repo şu anda yalnızca proje iskeletini içerir. İşlevler sonraki aşamalarda eklenecektir.

## Gerekenler

- .NET 8 SDK veya üzeri
- Git
- MySQL 8 veya üzeri (veritabanı aşamasında kullanılacak)

## İlk aşamayı çalıştırma

1. `dotnet --version` komutuyla SDK'nın kurulu olduğunu kontrol et.
2. Repo kökünde `dotnet restore MeetingRoomBooking.sln` çalıştır.
3. `dotnet build MeetingRoomBooking.sln` çalıştır.
4. `dotnet run --project src/MeetingRoomBooking.Api --urls http://localhost:5080` çalıştır.
5. Tarayıcıda `http://localhost:5080/swagger` ve `http://localhost:5080/api/status` adreslerini aç.

`src/MeetingRoomBooking.Api/appsettings.Example.json` yalnızca ayarların biçimini gösterir. Gerçek bağlantı ve JWT sırları sonraki aşamalarda yerel ayarlara eklenecek, Git'e yüklenmeyecektir.

Bu iskelette veritabanı, kimlik doğrulama, rezervasyon ve arayüz henüz uygulanmadı.
