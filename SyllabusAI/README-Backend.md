# SyllabusAI Backend (İlk Aşama – Login)

Raporla uyumlu: ortak giriş sayfası (Figure 1), email/şifre ile DB kontrolü, JWT ve rol tabanlı erişim (Student, Instructor, Admin).

Varsayılan: giriş **veritabanı** ile yapılır (API → DB kontrolü). Zip’teki / arkadaşınızın DB’yi kullanmak için **ConnectionStrings:DefaultConnection** veya **SyllabusDb** değerini kendi sunucunuza göre ayarlayın.

**Demo mod (DB yokken sadece arayüz testi):** `appsettings.Development.json` içinde **UseDemoAuth: true** yaparsanız veritabanına bağlanmaz; @bahcesehir.edu.tr / @bau.edu.tr mailleri + **DemoPassword** (örn. Test123!) ile giriş deneyebilirsiniz.

## Çalıştırma

```bash
cd SyllabusAI
dotnet run
```

- Ana sayfa (giriş): http://localhost:5242/
- Swagger: http://localhost:5242/swagger
- Login API: `POST /api/auth/login` → Body: `{ "email": "...", "password": "..." }`
- Ayar (demo mod): `GET /api/auth/config`

Veritabanı açıkken (UseDemoAuth: false) seed kullanıcıları:
- **Öğrenci:** ogrenci@bahcesehir.edu.tr / Test123!
- **Eğitmen:** egitmen@bau.edu.tr veya hoca@ou.bau.edu.tr / Test123!

## SQL Veritabanını Bağlama

1. **Bu proje:** `ConnectionStrings:DefaultConnection` → LocalDB veya kendi SQL Server'ınız.
2. **SyllabusAI-master (zip) projesindeki DB:** O projede `SyllabusDb` ve Identity kullanılıyor. Bu projede aynı DB'yi kullanmak isterseniz:
   - `appsettings.json` içinde **ConnectionStrings:SyllabusDb** örneği var; sunucu adını (örn. `BILGISAYAR\\SQLEXPRESS`) kendi makinenize göre yazın.
   - **DefaultConnection** değerini `SyllabusDb` ile değiştirirseniz bu API o veritabanına bağlanır (şema farklıysa User/Role entity'lerini o projedeki Student/Instructor yapısına göre uyarlamanız gerekebilir).

3. Arkadaşınızın paylaştığı veritabanını indirip SQL Server'da oluşturun (veya script'i çalıştırın).
4. **ConnectionStrings:DefaultConnection** değerini güncelleyin:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=SUNUCU;Database=VERITABANI_ADI;User Id=KULLANICI;Password=SIFRE;TrustServerCertificate=True;"
   }
   ```

3. Eğer GitHub'taki şema farklıysa (tablo/kolon isimleri):
   - `Models/User.cs` ve `Models/Role.cs` alanlarını şemaya göre düzenleyin,
   - Gerekirse `Data/ApplicationDbContext.cs` içinde tablo/kolon eşlemesi yapın (`[Table("...")]`, `[Column("...")]`),
   - Veya EF Core migration ile kendi şemanızı üretip mevcut DB'ye uyarlayın.

4. Production'da **Jwt:Key** değerini güçlü ve gizli bir anahtar ile değiştirin; `appsettings.json`'ı repoda commit etmeyin (User Secrets kullanın).

## Dersler ve AI (Ana sayfa akışı)

- **Öğrenci:** Ders kodu girerek ders ekler (veritabanına kayıt). “Derslerim” listesinden bir derse tıklayınca o dersin **müfredatı** (syllabus) görüntülenir; **öne çıkarılan kelimeler** (highlight) sarı işaretlenir. Aynı sayfada **AI sohbet**: müfredat metnine göre soru sorar, basit dille cevap alır.
- **Eğitmen:** Ders ekler (ders kodu, ad, müfredat metni, isteğe bağlı highlight kelimeler). Öğrenciler bu ders kodunu girerek dersi listelerine ekleyebilir.
- **API:** `POST/GET /api/courses`, `POST /api/courses/enroll`, `GET /api/courses/my`, `GET /api/courses/{id}/syllabus`, `POST /api/chat/ask`.

**Ders ve AI özellikleri için veritabanı gerekir:** `UseDemoAuth: false` yapıp connection string’i ayarlayın; uygulama açılırken tablolar (Courses, Enrollments) oluşturulur.

## Proje Yapısı

- **Controllers:** AuthController, CoursesController, ChatController
- **Services:** AuthService, CourseService, AiService (müfredattan basit cevap)
- **Models:** User, Role, Course, Enrollment
- **Data/ApplicationDbContext.cs** – EF Core
- **wwwroot:** index.html (giriş), student.html, student-add.html, student-ai.html, student-course.html (müfredat + AI), instructor.html, css/app.css
- **docs/AI-EGITIMI.md** – Bu proje için AI’ı nasıl eğiteceğiniz / RAG ve prompt ile nasıl geliştireceğiniz (rapor için referans)
