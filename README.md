# گوشه‌یار | GoshehYar

دستیار شناور ویندوز: یک پنل کوچک روی لبه یا گوشه صفحه که با نزدیک شدن ماوس یا کلید میانبر باز می‌شود.
Floating desktop assistant panel for Windows 10/11 (C# / .NET 10 / WPF).

## وضعیت پروژه / Status
| مرحله | وضعیت |
|---|---|
| ۱ - هسته (پنل، مخفی/ظاهر شدن، میانبر، Tray، تنظیمات) | سورس آماده؛ Build و تست روی GitHub Actions |
| ۲ - ابزارها (Screenshot، ضبط صدا، موزیک، یادداشت، Backup) | انجام نشده |
| ۳ - ویجت‌ها (ساعت، تقویم، آب‌وهوا، To-Do، تایمر، ماشین‌حساب) | انجام نشده |
| ۴ - پیشرفته (مانیتور سیستم، Clipboard، میانبرها) | انجام نشده |
| ۵ - تست نهایی و انتشار | انجام نشده |

## دریافت برنامه
1. وارد تب **Actions** در GitHub شوید و آخرین اجرای موفق «Build Windows» را باز کنید.
2. از بخش Artifacts فایل `GoshehYar-Setup-x64` (نصب‌کننده) یا `GoshehYar-Portable-x64` (بدون نصب) را دانلود کنید.
3. با ساخت Tag مثل `v1.0.0` فایل‌ها به صورت خودکار در بخش **Releases** هم قرار می‌گیرند.

راهنمای استفاده: [docs/GUIDE.fa.md](docs/GUIDE.fa.md)

## ساخت از سورس
نیازمند .NET 10 SDK و ویندوز:
```
dotnet build GoshehYar.slnx -c Release
dotnet test src/GoshehYar.Tests
```

## ساختار
- `src/GoshehYar.Core` منطق مستقل از UI (تنظیمات، هندسه پنل، میانبر، تم‌ها، ترجمه)
- `src/GoshehYar.App` برنامه WPF (پنل، Tray، تنظیمات)
- `src/GoshehYar.Tests` تست‌های خودکار
- `installer/` اسکریپت Inno Setup، `.github/workflows/` ساخت خودکار

داده‌ها و تنظیمات: `%LOCALAPPDATA%\GoshehYar\` (فقط محلی؛ هیچ داده‌ای ارسال نمی‌شود).

سازنده: محمدطاها طاهریان - مجوز MIT
