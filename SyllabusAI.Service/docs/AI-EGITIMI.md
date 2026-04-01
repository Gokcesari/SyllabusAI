# SyllabusAI Projesi İçin AI Nasıl Eğitilir / Geliştirilir?

Bu dokümanda, projedeki AI asistanın müfredata dayalı soru-cevap için nasıl “eğitilebileceği” ve geliştirilebileceği özetleniyor. Raporunuzdaki **RAG (Retrieval-Augmented Generation)** ve **grounding** (sadece müfredat içeriğine dayanma) hedeflerine uygun seçenekler anlatılıyor.

---

## 1. Mevcut Durum (Basit Versiyon)

Şu an **AiService** müfredat metninde **anahtar kelime eşlemesi** yapıyor: Öğrenci soruyu yazar, sorudaki kelimeler müfredatta aranıyor, bulunan cümleler “Müfredata göre: ...” diye döndürülüyor. Gerçek bir dil modeli yok; bu yüzden “eğitim” değil, kural tabanlı bir ön versiyon.

---

## 2. AI’ı “Eğitmek” / Geliştirmek İçin Üç Yaklaşım

### A) RAG (Retrieval-Augmented Generation) – Önerilen

**Ne yapıyor:** Önce müfredat metninden ilgili parçalar **aranır** (retrieval), sonra bu parçalar bir **dil modeline** (ör. GPT) bağlam olarak verilir; model sadece bu bağlama dayanarak **basit bir dille** cevap üretir.

**Nasıl “eğitirsin”:**

1. **Veri:** Her dersin `SyllabusContent` metni zaten veritabanında. Ek “eğitim verisi” toplamana gerek yok; AI’ın bilgi kaynağı bu metin.
2. **Parçalama (chunking):** Müfredat metnini anlamlı bloklara böl (örn. paragraf veya 200–400 karakterlik parçalar). Bu blokları vektör veritabanına (embedding) yaz.
3. **Soru geldiğinde:**  
   - Öğrenci sorusunu embedding’e çevir.  
   - En benzer 2–3 müfredat parçasını getir (retrieval).  
   - Bu parçaları + soruyu modele prompt olarak ver: *“Sadece aşağıdaki müfredat metnine göre, öğrenci sorusunu basit bir dille cevapla. Müfredatta yoksa ‘Müfredatta bu bilgi yok’ de.”*
4. **Model:** OpenAI API, Azure OpenAI veya açık kaynak bir model (LLama, Mistral vb.) kullanılabilir. “Eğitim” yok; model hazır, siz sadece **bağlamı (müfredat parçalarını)** veriyorsunuz.

**Avantaj:** Müfredat dışına çıkmaz (grounding), raporla uyumlu. Ek eğitim verisi veya GPU’da model eğitmek gerekmez.

---

### B) Fine-tuning (İsteğe Bağlı, İleri Aşama)

**Ne yapıyor:** Hazır bir modeli, “soru + müfredat parçası → basit cevap” örnekleriyle **ince ayarlayarak** daha iyi cevap vermesini sağlarsın.

**Nasıl yapılır:**

1. **Örnek veri seti:**  
   - 50–200 örnek: (müfredat parçası + öğrenci sorusu) → (basit Türkçe cevap).  
   - Örnek: *“Müfredat: Devam sınırı 4 haftadır. Soru: Kaç devamsızlık hakkım var? Cevap: 4 haftaya kadar devamsızlık hakkın var.”*
2. **Format:** OpenAI fine-tuning formatı (JSONL) veya Hugging Face için benzer format.
3. **Eğitim:** OpenAI’da “fine-tune” veya Hugging Face’te LoRA/QLoRA ile küçük bir model eğitimi. Bu aşamada GPU veya bulut API maliyeti gerekir.
4. **Sistemde kullanım:** Eğitilmiş modeli API’ye bağlarsın; yine **müfredat parçasını** her soruda bağlam olarak göndermek iyi olur (RAG + fine-tune birlikte).

**Ne zaman mantıklı:** RAG ile cevaplar yeterince iyi değilse veya proje raporunda “model eğitimi” istiyorsanız.

---

### C) Prompt Mühendisliği (En Hızlı Adım)

**Ne yapıyor:** Modeli eğitmeden, **sadece prompt metnini** iyi yazarak “sadece müfredata göre, basit dille cevapla” davranışını sağlarsın.

**Nasıl yapılır:**

1. **System prompt örneği:**  
   *“Sen bir üniversite müfredat asistanısın. Sadece verilen müfredat metnindeki bilgiye dayanarak cevap ver. Cevabı lise seviyesinde, basit Türkçe ile yaz. Müfredatta yoksa ‘Bu bilgi müfredatta yer almıyor’ de. Tahmin yapma.”*
2. **Her istekte:**  
   - Önce müfredattan ilgili parça(lar)ı getir (basit anahtar kelime veya RAG).  
   - Bu parça(lar) + öğrenci sorusunu **user message** olarak gönder.  
3. **Model:** Herhangi bir chat API (OpenAI, Azure, vb.). “Eğitim” yok; sadece prompt + bağlam.

Bu, projede **hemen** kullanılabilecek en pratik “eğitim benzeri” kontrol: AI’ı prompt ile “sadece müfredata bağlı ve basit dil” kuralına zorluyorsunuz.

---

## 3. Proje İçin Önerilen Sıra

| Aşama | Ne yapılır | “Eğitim” anlamı |
|--------|------------|------------------|
| 1 | **Prompt + mevcut AiService:** Müfredat metninden parça seç (kelime eşleme veya basit benzerlik), bu parçayı + soruyu ileride eklenecek bir API’ye gönderecek şekilde hazırla. | Yok; davranış prompt ile belirlenir. |
| 2 | **RAG:** Müfredatı chunk’la, embedding + vektör DB ekle; soru → en alakalı chunk’ları getir → dil modeline ver. | Yok; model hazır, bağlam müfredat. |
| 3 | **OpenAI / Azure entegrasyonu:** RAG’dan gelen metni + soruyu API’ye gönder, cevabı öğrenciye döndür. | Yok; sadece kullanım. |
| 4 | (İsteğe bağlı) **Fine-tuning:** Örnek soru–cevap verisi topla, küçük bir modeli ince ayarlayıp RAG ile birlikte kullan. | Var; model müfredat tipi soru–cevaplara özelleşir. |

---

## 4. Kısa Özet

- **“AI’ı eğitmek”** bu projede iki anlama gelebilir:  
  - **(1)** Modeli fine-tune etmek (ileri aşama, örnek veri + eğitim).  
  - **(2)** Sistemi müfredata “bağlamak” (RAG + iyi prompt) – asıl hedef bu; raporunuzdaki “grounding” ve “basit dille cevap” böyle sağlanır.
- **Hemen uygulanabilir:** RAG + prompt (ve isteğe bağlı OpenAI/Azure).  
- **İleride:** İsterseniz örnek soru–cevaplarla fine-tuning ekleyebilirsiniz; dokümandaki adımlar bu proje için uygun bir yol haritası niteliğindedir.

Bu rehber, proje raporunda “AI’ın nasıl eğitildiği / müfredata nasıl bağlandığı” bölümünde referans olarak kullanılabilir.
