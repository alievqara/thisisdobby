# Dobby · Homelab Control

Telegram üzərindən Docker Compose tətbiqlərini aşkar et, status/health və loqları gör, GitHub-dan yenilə.

## Quraşdırma

.NET 9, Docker Compose v2 və Git tələb olunur. Botu homelab serverində işlət; lokal kompüterdə işlətmək uzaq serveri avtomatik idarə etmir.

Token və admin ID-ni mövcud `.env` faylında saxla:

```
DobbyBot__Token=YOUR_BOT_TOKEN
DobbyBot__AdminTelegramId=YOUR_TELEGRAM_ID
```

`DobbyBot.Worker/appsettings.Homelab.example.json` nümunəsinin məzmununu botun iş qovluğunda `homelab.json` kimi saxla. Bu serverdə yol: `/srv/homelab/secrets/thisisdobby/homelab.json`. Bu fayl release-lərdən kənarda qalır. Dəyişəndən sonra botu restart et. Mühit dəyişənləri JSON sazlamalarından üstündür. Nümunə özü aktiv deyil. Planzy üçün serverdəki həqiqi repo yolunu, branch-i və Compose faylını yaz. `ProjectName` mövcud Compose project adı ilə eyni olmalıdır (`docker compose ls --all`). Windows serverində tam Windows yolundan istifadə et.

GitHub SSH açarı və ya credential helper botu işlədən istifadəçi üçün əvvəlcədən sazlanmalıdır. Tokeni repo URL-inə yazma. Compose faylı daxilindəki build/image, env və volume sazlamaları tətbiqin öz reposundan gəlir.

```
dotnet build DobbyBot.slnx
dotnet run --project DobbyBot.Worker
```

## Telegram axını

`/menu` → Tətbiqlər → Planzy → GitHub-dan yenilə → Təsdiqlə.

Bütün Compose layihələri, o cümlədən dayanmış layihələr, hər açılışda yenidən aşkarlanır. Repo siyahısına əlavə edilən tətbiqlər quraşdırılmayıbsa da görünür. Tək konteynerlər ayrıca Konteynerlər bölməsində görünür; systemd/native proqramlar Apps siyahısına daxil deyil.

Yeniləmə təmiz iş qovluğu və düzgün aktiv branch tələb edir. `git pull --ff-only origin BRANCH`, image build/pull və `docker compose up --wait` icra olunur. Lokal dəyişikliklər silinmir, divergent tarix məcburi dəyişdirilmir. Healthcheck yalnız Compose-da təyin edilibsə sağlamlığı təsdiqləyir; əks halda konteynerin işləməsi yoxlanır. Avtomatik rollback və verilənlər bazası miqrasiyası yoxdur. Miqrasiyalar tətbiqin deployment axınında müəyyən edilməlidir. Yeniləmə qısa fasilə yarada bilər.

Başlat/restart/dayandır və yeniləmə yalnız açıq şəkildə konfiqurasiya edilmiş tətbiqlərə aiddir. Təsdiq istifadəçiyə bağlıdır, birdəfəlikdir və 2 dəqiqə sonra bitir. Əməliyyatlar arxa planda növbə ilə icra olunur; bot cavab verməyə davam edir. Eyni tətbiq üçün təkrar növbə qəbul edilmir. Növbədə ən çox 8 əməliyyat, tarixçədə ən çox 60 qeyd saxlanır. Nəticə əməliyyatı başladan söhbətə göndərilir. Tarixçə yalnız aid olduğu adminə görünür və restart zamanı silinir; gözləyən işlər restartdan sonra davam etmir. Bütün əməliyyata 30 dəqiqə, Git/build/pull/up üçün 15 dəqiqə, digər subprocess-lərə 1 dəqiqə limit var. Loqlar Telegram mesaj limitinə uyğun qısaldılır.

Bot yalnız AdminTelegramId istifadəçisini qəbul edir. Docker/Git əməliyyatları bot prosesinin icazələri ilə işləyir. Canlı serverə keçməzdən əvvəl ehtiyat nüsxə və tətbiqin öz healthcheck-lərini sazla.

## Yoxlama

Avtomatik yoxlamalar: dotnet run --project tests/DobbyBot.Checks

Yoxlamalar Docker-i və deployment üçün Git cavablarını simulyasiya edir; subprocess oxuma/xəta yoxlamaları həqiqi Git ilə işləyir. Canlı deployment etmir.

## Yeni idarəetmə paneli

- Əsas panel: konteyner sayı, işləyən/dayanan say, unhealthy vəziyyəti, sistem və bot uptime, Linux RAM/load.
- Resurslar: konteynerlərin CPU, RAM və şəbəkə istifadəsinin bir anlıq göstəriciləri.
- Problemlər: unhealthy, restarting və dead konteynerlər; planlı dayanmış konteynerlər ayrıca göstərilir. Bu səhifə açıldıqda yoxlayır, avtomatik monitor deyil.
- Docker disk istifadəsi, şəbəkələr və engine diaqnostikası.
- Tətbiqlərdə 8 elementlik səhifələr, qarışıq running/exited vəziyyətinin fərqli rəngi.
- Status/loqlar Docker label-ləri ilə alınır, yerli Compose faylı tələb etmir.
- Yeniləmə yoxla: origin-dən fetch edir, lokal/remote commit fərqini göstərir; iş qovluğunu və konteynerləri dəyişmir.
- Əməliyyatlar: növbə, gediş mərhələsi, nəticə, UTC vaxtı və əməliyyat ID-si.
- Downloader sonra tamamlanacaq; hazır olmayan düymələr aktiv funksiyalar kimi göstərilmir.

## Serverdə yeni bot versiyasına keçid

Server üçün təkrar istifadə edilən skript: `scripts/deploy-server.sh`. `sudo -v` yoxlamasını build-dən əvvəl edir, DLL-i dobby istifadəçisi ilə həqiqətən açıb yoxlayır, sonra current keçidini dəyişir. Servis başlamasa əvvəlki release-ə qayıdır. `.env` və `homelab.json`-a toxunmur. Yoxlama systemd prosesinin aktivliyini təsdiqləyir; Telegram-da `/menu` ilə ayrıca yoxla.

Source checkout-u dəyişmədən GitHub-dakı skripti götürüb işlət:

```bash
git -C /srv/homelab/apps/thisisdobby/source fetch origin main
git -C /srv/homelab/apps/thisisdobby/source show origin/main:scripts/deploy-server.sh > /tmp/dobby-deploy-server.sh
bash /tmp/dobby-deploy-server.sh
```
