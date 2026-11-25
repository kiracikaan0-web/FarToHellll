# FarToHell – UI Görselleri Rehberi (Tasarımcı için)

Oyundaki bütün menü görselleri, fontları ve renkleri **tek bir dosyadan** yönetiliyor:

**`Assets/UI/Resources/UITheme.asset`**

Bu dosyayı Unity'de seçince Inspector'da tüm kutuları görürsün. Yeni bir görseli ilgili kutuya sürükleyip bırakman yeterli. Ana menü ve duraklatma menüsü kendiliğinden güncellenir, sahneye dokunmana gerek yok.

## Görsel kutuları

| Kutu | Nerede görünür | Önerilen boyut | Not |
|---|---|---|---|
| **Menu Title** | Ana menü başlığı (logo) | ~2000 × 500 px, şeffaf PNG | Ekranda yaklaşık 1250 px genişliğinde gösterilir |
| **Menu Vignette** | Ana menü kenar karartması | 512 × 288 px veya daha büyük, şeffaf PNG | Tüm ekrana gerilir |
| **Pause Panel** | Duraklatma menüsü arka paneli | 820 × 760 px, şeffaf PNG | Boş bırakılırsa panel gizlenir |
| **Pause Title** | Duraklatma başlığı | ~700 × 140 px, şeffaf PNG | **Boş bırakılırsa "DURAKLATILDI" yazısı** kullanılır |
| **Button Deco** | Seçili butonun iki yanındaki süs | 64 × 64 px, şeffaf PNG | Boş bırakılırsa küçük elmas çizilir |
| **Button Background** | Butonun arka planı | 600 × 100 px, şeffaf PNG | Boş bırakılırsa buton arka plansız, sadece yazı |
| **Hud Bar Frame** | Can ve stamina barlarının çerçevesi | ~420 × 34 px, şeffaf PNG, **9-slice** (Sprite Editor'de kenar çiz) | Boş bırakılırsa ince çizgi kullanılır. Bar uzayınca çerçeve de bozulmadan uzar |

## Font ve renkler

- **Button Font / Title Font:** `.ttf` veya `.otf` dosyasını `Assets/Fonts/` klasörüne koy ve kutuya sürükle. Fontun **Türkçe karakterleri** (Ş, İ, Ğ, Ç, Ö, Ü) içerdiğinden emin ol, yoksa bu harfler görünmez.
- **Button Normal / Selected:** butonun normal ve seçiliyken yazı rengi.
- **Deco Color:** süslerin rengi. Kendi süs görselini koyduysan beyaz yapabilirsin, böylece görselin orijinal rengi görünür.
- **Title Color / Hint Color:** başlık ve alt bilgi yazıları.
- **Pause Overlay:** duraklatınca oyunun üstüne gelen karartmanın rengi ve şeffaflığı.
- **Hud Health / Hud Health Chip / Hud Stamina:** can barı, hasar alınca geride kalan sarı "kayıp" barı ve stamina barı renkleri. **Hud Stamina Empty:** stamina yetmeyince yanıp sönen renk. **Hud Bar Back / Border:** barların boş kısmı ve kenar çizgisi.

## Görsel import ayarları

Yeni bir PNG ekleyince Inspector'da şunları yap:
1. **Texture Type:** Sprite (2D and UI)
2. **Compression:** None (sıkıştırma kaliteyi bozar)
3. **Apply**

## Dosya yerleri

- Görseller: `Assets/UI/`
- Fontlar: `Assets/Fonts/`
- Oyun içi arka plan katmanları: `Assets/Sprites/Background/` (aynı adla değiştirmen yeterli)

## Menüleri baştan kurmak

Unity menüsünden:
- **Tools → FarToHell → Ana Menu Kur**
- **Tools → FarToHell → Duraklatma Menusu Kur**
- **Tools → FarToHell → Can ve Stamina Kur** (HUD barları)

Bunlar menüleri temadaki görsellerle yeniden oluşturur. UITheme dosyasına dokunmazlar, yani senin görsellerin korunur.
