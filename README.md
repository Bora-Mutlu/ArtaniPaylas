# ArtaniPaylas

ArtaniPaylas, artan gida ilanlarinin ihtiyac sahiplerine guvenli ve hizli ulasmasi icin gelistirilmis bir ASP.NET Core MVC projesidir.

## Proje Amaci

MVP kapsaminda kullanicilarin:

- hesap olusturmasi ve giris yapmasi,
- gida ilani olusturmasi ve yonetmesi,
- ilanlara talep gondermesi,
- ilan sahibinin talepleri onaylamasi/reddetmesi/teslim etmesi,
- teslim sonrasi puan ve yorumla degerlendirme yapmasi

saglanmistir.

## Teknoloji Yigini

- .NET 8
- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core 8
- PostgreSQL (Npgsql)

## Cozum Yapisi

- `ArtaniPaylas.Core`
Domain entity'leri, enum'lar, view model'ler ve servis arayuzleri.

- `ArtaniPaylas.Data`
`ApplicationDbContext`, EF Core iliski/mapping ayarlari, migration dosyalari ve veri servisleri.

- `ArtaniPaylas.Web`
Controller'lar, Razor View'ler, middleware ve uygulama giris noktasi (`Program.cs`).

## Temel Ozellikler

- Kimlik ve yetkilendirme
  - Kayit, giris, cikis
  - Parola politikasi ve hesap kilitleme kurallari

- Ilan yonetimi
  - Ilan olusturma, guncelleme, silme
  - Listeleme, arama/filtreleme, detay sayfasi
  - Ilan sahiplik kontrolu

- Talep sureci
  - Ilana talep gonderme
  - Gelen/Giden talepleri goruntuleme
  - Onay/Red/Teslim adimlari

- Profil ve guven skoru
  - Profil duzenleme
  - Profil resmi yukleme
  - Teslim sonrasi puan/yorum
  - Ortalama trust score hesaplama

- Guvenlik
  - Global Anti-Forgery dogrulamasi
  - Cookie guvenlik ayarlari
  - Rate limiting
  - Security headers middleware
  - Dosya yukleme dogrulama

## Kurulum

1. Depoyu klonlayin:

```bash
git clone https://github.com/Bora-Mutlu/ArtaniPaylas.git
cd ArtaniPaylas
```

2. Bagimliliklari geri yukleyin:

```bash
dotnet restore
```

3. Veritabani baglantisini ayarlayin (`ArtaniPaylas.Web/appsettings.json` veya user-secrets).

Ornek:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
}
```

4. Migration'lari uygulayin:

```bash
dotnet ef database update --project ArtaniPaylas.Data --startup-project ArtaniPaylas.Web
```

5. Uygulamayi calistirin:

```bash
dotnet run --project ArtaniPaylas.Web
```

## Yol Haritasi

- Bildirim sistemi (e-posta/uygulama ici)
- Rol bazli kurum yonetim paneli
- Test kapsam artisi (unit + integration)
- Medya dosyalari icin bulut depolama

## Dokumanlar

- Haftalik analiz ve plan dosyalari: `docs/`
- 6 haftalik rapor: `docs/6-haftalik-rapor.html`

