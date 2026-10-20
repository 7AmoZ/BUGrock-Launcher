# تقرير الفحص الشامل لمشروع BUGrock Launcher

> [!IMPORTANT]
> تم إعداد هذا التقرير بعد فحص فعلي وكامل لجميع ملفات المشروع (`C#`، `XAML`، `C++`، ملفات الحلول والمشاريع، وملف البناء في GitHub Actions) دون إجراء أي تعديل على الكود حتى الآن، بانتظار إشارة البدء (`go`).

---

## 1. حصر مصطلحات `MCLauncher` للتخلص منها على مستوى الكود

جميع المواضع التالية لا تزال تحمل الاسم القديم `MCLauncher` وسيتم تحويلها إلى `BUGrockLauncher` (مع الحفاظ على اسم الملف التنفيذي `BUGrock_Launcher.exe`):

| الفئة | الملف | الموضع / التفاصيل | التعديل المطلوب |
| :--- | :--- | :--- | :--- |
| **ملف الحل (Solution)** | `MCLauncher.sln` | اسم ملف الحل واسم ومجلد المشروع المرجعي | إعادة التسمية إلى `BUGrockLauncher.sln` وتحديث مرجع المشروع |
| **ملف المشروع (CSProj)** | `MCLauncher/MCLauncher.csproj` | `<RootNamespace>MCLauncher</RootNamespace>` | تحويله إلى `<RootNamespace>BUGrockLauncher</RootNamespace>` |
| **GitHub Actions** | `.github/workflows/build.yml` | يشير إلى `MCLauncher.sln` في خطوات `nuget restore` و `msbuild` | تحديث المسار إلى `BUGrockLauncher.sln` |
| **Manifest** | `MCLauncher/app.manifest` | `name="MyApplication.app"` | تحديثه إلى `name="BUGrockLauncher.app"` |
| **التوثيق** | `README.md` | العنوان `# MCLauncher` والإشارة إلى `MCLauncher.exe` | تحديثه إلى `# BUGrock Launcher` و `BUGrock_Launcher.exe` |
| **مشاريع C++** | `GDKDecryptHelper/GDKDecryptHelper.cpp` | `GetTempFileNameW(tempPath, L"MCLauncher", 0, tempFile)` | إزالة الكود الميت الذي ينشئ ملفات مؤقتة باسم `MCLauncher` |
| **واجهات XAML** | `App.xaml`، `MainWindow.xaml`، `InstallLocationDialog.xaml`، `ProgressDialog.xaml`، `SplashScreenWindow.xaml`، `ThemedMessageBox.xaml`، `VersionListEndpointDialog.xaml`، `WorldConflictDialog.xaml`، `SampleData/Versions.xaml` | `x:Class="MCLauncher.*"` و `xmlns:local="clr-namespace:MCLauncher*"` | تحديثها إلى `BUGrockLauncher` و `BUGrockLauncher.WPFDataTypes` و `BUGrockLauncher.SampleData` |
| **ملفات C#** | جميع ملفات `.cs` داخل مجلد المشروع (`App.xaml.cs`، `MainWindow.xaml.cs`، `LinkResolver.cs`، `Preferences.cs`، `RelayCommand.cs`، `SteveHeadHelper.cs`، `VersionDownloader.cs`، `VersionList.cs`، `WUProtocol.cs`، `WUTokenHelper.cs`، `SampleData/SampleClasses.cs` وملفات الـ Dialogs) | `namespace MCLauncher` | تحويلها بالكامل إلى `namespace BUGrockLauncher` |

---

## 2. الأخطاء البرمجية المكتشفة (Bugs)

### أ. أخطاء واجهة المستخدم والـ Threading (C# / WPF)

1. **اختفاء الأزرار تماماً في `ThemedMessageBox` عند استخدام `MessageBoxButton.YesNoCancel`:**
   - **الموقع:** `ThemedMessageBox.SetupButtons` و `MainWindow.CheckGdkPrerequisitesAndWarn`.
   - **السبب:** دالة `SetupButtons` تفحص فقط `OK` و `OKCancel` و `YesNo`، ولا تحتوي على فرع لـ `YesNoCancel`. عند استدعائها في `CheckGdkPrerequisitesAndWarn` تظهر النافذة للمستخدم بدون أي زر!
   - **مشكلة إضافية في نفس الملف:** إغلاق نافذة من نوع `YesNo` عبر زر `✕` (`Close_Click`) يُرجع `MessageBoxResult.Cancel` بدلاً من `MessageBoxResult.No`.

2. **انهيار اللانشر (`InvalidOperationException: The calling thread must be STA`) عند إظهار رسائل خطأ من الخلفية:**
   - **الموقع:** `ThemedMessageBox.Show` عند استدعائه من داخل `Task.Run` في `InvokeLaunch`، `InvokeDownload`، `Remove`، و `RestoreMove` / `RestoreUWPData` / `MoveMinecraftData`.
   - **السبب:** `ThemedMessageBox.Show` ينشئ نافذة WPF (`new ThemedMessageBox(...)`) مباشرة دون التحقق مما إذا كان الخيط الحالي هو خيط الواجهة (`Application.Current.Dispatcher.CheckAccess()`).
   - **الحل:** تغليف `ThemedMessageBox.Show` بـ `Dispatcher.Invoke` تلقائياً عندما يُستدعى من خيط خلفي.

3. **عدم تجميد فرش الألوان الثابتة (`SolidColorBrush`) في `VersionStateChangeInfo`:**
   - **الموقع:** `VersionStateChangeInfo` في `MainWindow.xaml.cs`.
   - **السبب:** الفرش `_greenBrush` و `_greenBadgeBrush` و `_magentaBrush` و `_magentaBadgeBrush` معرّفة كـ `static readonly` دون استدعاء `.Freeze()`. إذا تم الوصول للكلاس لأول مرة من داخل `Task.Run`، ترتبط الفرش بخيط الخلفية وتسبب انهيار WPF عند ربطها بالواجهة.

4. **مسح رابط GDK Endpoint في نافذة `VersionListEndpointDialog`:**
   - **الموقع:** `VersionListEndpointDialog.xaml.cs` و `MenuItemSetVersionListEndpointClicked`.
   - **السبب:** الـ Constructor يستقبل `currentEndpoint` الخاص بـ UWP فقط ولا يملأ `GDKUrlsEndpointTextBox.Text`، وعند الحفظ يُرسل قيمة فارغة تمسح رابط الـ GDK المحفوظ في الإعدادات.

5. **كتابة شارة `RELEASE` بشكل ثابت في قوائم الإصدارات:**
   - **الموقع:** `MainWindow.xaml` (السطر 242 والسطر 273).
   - **السبب:** في القالبين `templateVersionNotInstalled` و `templateVersionInstalled` تم وضع `Text="RELEASE"` بدلاً من `{Binding DisplayVersionType}` (بينما القالب الثالث `templateVersionDownloading` في السطر 309 يستخدم الـ Binding الصحيح).

---

### ب. أخطاء التحميل وفك الحزم والتثبيت

6. **عدم استخدام `_userVersionDownloader` لتحميل نسخ الـ Beta:**
   - **الموقع:** `MainWindow.xaml.cs` (الأسطر 49-51، 116-118، 1359).
   - **السبب:** رغم تجهيز `_userVersionDownloader` و `_userVersionDownloaderLoginTask` لتسجيل الدخول بجلب التوكن عبر `WUTokenHelper` لتحميل نسخ الـ Beta، فإن الدالة `InvokeDownload` في السطر 1359 تستخدم `_anonVersionDownloader` دائماً ولا تشغّل مهمة تسجيل الدخول أبداً.

7. **تجاهل الإلغاء أثناء فك الضغط بعد التحميل وحذف الحزمة المحمّلة رغم فشل التثبيت:**
   - **الموقع:** `InvokeDownload` في `MainWindow.xaml.cs` (الأسطر 1392-1404).
   - **السبب:**
     1. لا يتم تمرير `cancelSource.Token` إلى `ExtractAppx` و `ExtractMsixvc`.
     2. لا يتم التحقق من نجاح `ExtractAppx` / `ExtractMsixvc` قبل تنفيذ `File.Delete(dlPath)`، مما يؤدي لحذف ملف اللعبة الذي تم تحميله حتى لو فشل فك الضغط أو التثبيت!

8. **غياب فحص حالة HTTP (`EnsureSuccessStatusCode`) وعدم التبديل للرابط الاحتياطي في `VersionDownloader`:**
   - **الموقع:** `VersionDownloader.cs` (الأسطر 29-87).
   - **السبب:**
     1. `DownloadFile` لا يستدعي `resp.EnsureSuccessStatusCode()`، فإذا أعاد السيرفر صفحة خطأ `404` أو `503` يتم حفظها كملف `.appx`/`.msixvc`.
     2. `PostXmlAsync` لا يستدعي `resp.EnsureSuccessStatusCode()` قبل محاولة تحليل الـ XML.
     3. `DownloadMsixvc` يجرب `downloadUrls[0]` فقط ولا يحاول التحميل من المرآة الاحتياطية `downloadUrls[1]` عند فشل الرابط الأول، كما يفتقر لفحص ما إذا كانت القائمة فارغة.

9. **تعارض أكواد الأخطاء بين C++ و C# في `WUTokenHelper`:**
   - **الموقع:** `WUTokenHelper/main.cpp` (السطر 12) و `WUTokenHelper.cs` (الأسطر 26-46).
   - **السبب:**
     1. في C++: `WU_TOKEN_FETCH_ERROR_BASE = 0x80040400`، بينما في C#: `WU_TOKEN_FETCH_ERROR_BASE = 0x80040300` و `WU_ERRORS_END = 0x80040400`، وبالتالي تقع جميع أخطاء `WebTokenRequestStatus` خارج النطاق!
     2. في السطر 43 من `WUTokenHelper.cs`: يتم استدعاء `Enum.IsDefined(typeof(WebTokenRequestStatus), e)` بتمرير الكود السالب `e` بدلاً من `(int)actualCode`، فيفشل الشرط دائماً ويعرض `"THIS IS A BUG"`.
     3. في `WUTokenHelper/main.cpp`: غياب `try/catch` حول دوال WinRT داخل دالة `extern "C"` قد يسبب `SEHException` عند انقطاع الإنترنت أو غياب حساب Microsoft.

10. **الاعتماد على المسار النسبي لملف `GDKLaunchShim.exe`:**
    - **الموقع:** `ReRegisterPackage` في `MainWindow.xaml.cs` (الأسطر 1291-1295).
    - **السبب:** استدعاء `CreateHardLink(shimPath, GDK_SHIM_NAME, ...)` و `File.Copy(GDK_SHIM_NAME, ...)` يعتمد على مجلد العمل الحالي بدلاً من `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GDK_SHIM_NAME)`.

---

### ج. تسريبات الموارد وأخطاء الـ C++ والـ P/Invoke

11. **تسريب مقبض الملف (`SafeFileHandle`) وعدم توسيع الـ Buffer في `LinkResolver.cs`:**
    - **الموقع:** `LinkResolver.Resolve` (الأسطر 34-47).
    - **السبب:** `SafeFileHandle handle = CreateFile(...)` لا يتم تحريره بـ `using`، وإذا زاد طول المسار عن 1024 حرفاً لا يتم إعادة المحاولة بـ `StringBuilder` أكبر.

12. **تسريب ملفات مؤقتة في `GDKDecryptHelper.cpp`:**
    - **الموقع:** `GDKDecryptHelper.cpp` (الأسطر 40-50).
    - **السبب:** استدعاء `GetTempFileNameW` ينشئ ملفاً فعلياً على القرص في `%TEMP%` لا يُستخدم ولا يُحذف أبداً.

13. **أخطاء متعددة وتسريبات موارد في `GDKLaunchShim.cpp`:**
    - **الموقع:** `GDKLaunchShim.cpp` (الأسطر 70-225).
    - **التفاصيل:**
      - في `getTitleId` (السطر 72): الملف `FILE* fp` المفتوح عبر `_wfopen_s` لا يُغلق أبداً بـ `fclose(fp)`.
      - في `getTitleId` (السطر 95): استخدام `std::stoi(titleIdElement->GetText(), nullptr, 16)` ينهار إذا كانت `GetText()` تساوي `nullptr` أو إذا كان الـ `TitleId` يتجاوز `0x7FFFFFFF` (`std::out_of_range`). يجب استخدام `std::strtoul` مع فحص `nullptr`.
      - في `findMinecraftProcess` (الأسطر 108-120): تسريب `HANDLE hproc` عند فشل `QueryFullProcessImageName` لأن `continue` يتم قبل `CloseHandle(hproc)`.
      - في `activateRunningInstance` (السطر 149): خطأ في سلسلة الهروب `L"\\?\\\\"` (المقصود `L"\\\\?\\"` لفحص البادئة `\\?\`).
      - في `activateRunningInstance` (السطر 155): عدم تحرير `HSTRING` عبر `WindowsDeleteString`.

---

## 3. نقاط الضعف والثغرات الأمنية والاعتمادية (Vulnerabilities & Weaknesses)

1. **ثغرة Zip Slip الجزئية عند فك ضغط `.appx`:**
   - **الموقع:** `ExtractAppx` في `MainWindow.xaml.cs` (الأسطر 308-311).
   - **التفاصيل:** الفحص `destinationPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase)` يتم بدون ضمان انتهاء `fullTargetDir` بفاصل مجلدات `\`. إذا كان المجلد الهدف `C:\Games\Minecraft-1.20`، فإن مساراً خبيثاً مثل `..\Minecraft-1.20-malicious\file.dll` سيجتاز الفحص.

2. **انكسار أمر PowerShell عند وجود علامة اقتباس مفردة `'` في المسار وعدم تنظيف الملفات المؤقتة عند الفشل:**
   - **الموقع:** `ExtractMsixvc` في `MainWindow.xaml.cs` (الأسطر 651-661).
   - **التفاصيل:**
     - السلسلة `-Args '"{exeSrcPath}" ...'` داخل أمر PowerShell تنكسر إذا كان مسار المستخدم أو اللانشر يحتوي على علامة اقتباس مفردة `'` (مثل `C:\Users\Ahmed's PC\...`). يجب مضاعفة علامة الاقتباس المفردة (`Replace("'", "''")`).
     - الملفات المؤقتة (`decryptHelperLogFile` و `exeTmpPath` و `donePath`) لا يتم حذفها في `finally` إذا خرجت الدالة مبكراً بسبب خطأ أو إلغاء.

3. **غياب الحماية عند تلف ملفات JSON (`preferences.json` / `versions_*.json`):**
   - **الموقع:** `MainWindow` Constructor (الأسطر 57-59) و `VersionList.cs` (الأسطر 88-128).
   - **التفاصيل:**
     - إذا تلف ملف `preferences.json`، ينهار اللانشر فور فتحه قبل ظهور النافذة.
     - في `VersionList.cs`، الدالتان `LoadFromCacheUWP` و `LoadFromCacheGDK` تلتقطان `FileNotFoundException` فقط ولا تتعاملان مع تلف ملف الكاش، كما أن `ParseDataGDK` لا يتحقق من كون `data["release"]` أو `data["preview"]` غير خاليين (`null`).
     - في `VersionList.AddEntry` (السطر 148): يتم إزالة `.appx` فقط (`name.Replace(".appx", "")`) دون إزالة `.msixvc` عند استيراد حزمة GDK.

4. **تداخل استدعاء `LoadVersionList` (Race Condition):**
   - **الموقع:** `LoadVersionList` في `MainWindow.xaml.cs` (السطر 123).
   - **التفاصيل:** الضغط على تحديث قائمة الإصدارات أثناء عمل التحميل الأولي يؤدي لتشغيل دالتي `LoadVersionList` بالتوازي وتعديل `_versions` في نفس الوقت.

---

## 4. خطة ترتيب الكود وتنفيذ الإصلاحات (على 4 مراحل متتالية)

1. **المجموعة الأولى — التخلص من `MCLauncher` وترتيب الكود:**
   - تحويل كافة الـ Namespaces وملفات XAML وملفات الحلول والمشاريع والـ Workflow إلى `BUGrockLauncher`.
   - فصل كلاسات البيانات (`WPFDataTypes`: `Version`, `VersionStateChangeInfo`, `ChromeSpeedCalculator`, `MinecraftPackageFamilies`, إلخ) من ملف `MainWindow.xaml.cs` (البالغ 2,745 سطراً) إلى ملفات مستقلة ومنظمة.
2. **المجموعة الثانية — إصلاح مشاريع الـ C++ و P/Invoke:**
   - إصلاح `GDKDecryptHelper.cpp`، و `GDKLaunchShim.cpp`، و `WUTokenHelper/main.cpp`، و `WUTokenHelper.cs`، و `LinkResolver.cs`.
3. **المجموعة الثالثة — إصلاح التحميل والكاش والثغرات الأمنية:**
   - إصلاح `VersionDownloader.cs`، و `VersionList.cs`، وحماية قراءة `preferences.json`، وسد ثغرة Zip Slip في `ExtractAppx`، وتأمين أمر PowerShell وتنظيف الملفات المؤقتة في `ExtractMsixvc`.
4. **المجموعة الرابعة — إصلاح الواجهة والـ Threading والتحقق بالبناء:**
   - إصلاح `ThemedMessageBox` (`YesNoCancel` + `Dispatcher`)، وتجميد الـ Brushes، وإصلاح `VersionListEndpointDialog`، وإصلاح شارات `MainWindow.xaml`، وإصلاح `InvokeDownload` و `LoadVersionList`، ثم تشغيل البناء الكامل عبر GitHub Actions للتحقق الفعلي من نجاح كافة التعديلات بدون أخطاء.
