# Dobby · Homelab Control

Telegram üzərindən Docker Compose tətbiqlərini aşkar et, status/health və loqları gör, GitHub-dan yenilə.

## Quraşdırma

.NET 9, Docker Compose v2 və Git tələb olunur. Botu homelab serverində işlət; lokal kompüterdə işlətmək uzaq serveri avtomatik idarə etmir.

Token və admin ID-ni mövcud `.env` faylında saxla:

```
DobbyBot__Token=YOUR_BOT_TOKEN
DobbyBot__AdminTelegramId=YOUR_TELEGRAM_ID
```

`DobbyBot.Worker/appsettings.Homelab.example.json` faylındakı nümunə əsasında `Homelab` bölməsini `appsettings.json`-a əlavə et. Nümunə özü aktiv deyil. Planzy üçün serverdəki həqiqi repo yolunu, branch-i və Compose faylını yaz. `ProjectName` mövcud Compose project adı ilə eyni olmalıdır (`docker compose ls --all`). Windows serverində tam Windows yolundan istifadə et.

GitHub SSH açarı və ya credential helper botu işlədən istifadəçi üçün əvvəlcədən sazlanmalıdır. Tokeni repo URL-inə yazma. Compose faylı daxilindəki build/image, env və volume sazlamaları tətbiqin öz reposundan gəlir.

```
dotnet build DobbyBot.slnx
dotnet run --project DobbyBot.Worker
```

## Telegram axını

`/menu` → Tətbiqlər → Planzy → GitHub-dan yenilə → Təsdiqlə.

Bütün Compose layihələri, o cümlədən dayanmış layihələr, hər açılışda yenidən aşkarlanır. Repo siyahısına əlavə edilən tətbiqlər quraşdırılmayıbsa da görünür. Tək konteynerlər ayrıca Konteynerlər bölməsində görünür; systemd/native proqramlar Apps siyahısına daxil deyil.

Yeniləmə təmiz iş qovluğu və düzgün aktiv branch tələb edir. `git pull --ff-only origin BRANCH`, image build/pull və `docker compose up --wait` icra olunur. Lokal dəyişikliklər silinmir, divergent tarix məcburi dəyişdirilmir. Healthcheck yalnız Compose-da təyin edilibsə sağlamlığı təsdiqləyir; əks halda konteynerin işləməsi yoxlanır. Avtomatik rollback və verilənlər bazası miqrasiyası yoxdur. Miqrasiyalar tətbiqin deployment axınında müəyyən edilməlidir. Yeniləmə qısa fasilə yarada bilər.

Başlat/restart/dayandır və yeniləmə yalnız açıq şəkildə konfiqurasiya edilmiş tətbiqlərə aiddir. Təsdiq istifadəçiyə bağlıdır, birdəfəlikdir və 2 dəqiqə sonra bitir. Eyni anda bir idarəetmə əməliyyatı işləyir. Hər subprocess üçün 15 dəqiqə limit var. Loqlar Telegram mesaj limitinə uyğun qısaldılır.

Bot yalnız AdminTelegramId istifadəçisini qəbul edir. Docker/Git əməliyyatları bot prosesinin icazələri ilə işləyir. Canlı serverə keçməzdən əvvəl ehtiyat nüsxə və tətbiqin öz healthcheck-lərini sazla.

## Yoxlama

Avtomatik yoxlamalar: dotnet run --project tests/DobbyBot.Checks

Yoxlamalar Docker/Git-i simulyasiya edir; canlı deployment etmir.
