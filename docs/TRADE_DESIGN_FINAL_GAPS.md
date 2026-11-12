# آخرین اصلاحات محدود طراحی Trade — پیش از تأیید فاز اول (TRADE DESIGN FINAL GAPS)

**تاریخ:** ۱۴۰۵/۰۸/۲۲ (۲۰۲۶-۱۱-۱۲)
**هدف این سند:** تعیین تکلیف سه ابهام محدود باقی‌مانده از `TRADE_DESIGN_FINAL_VERIFICATION.md`، پیش از تأیید نهایی شروع پیاده‌سازی فاز اول.
**دامنه:** فقط مستندات `docs/TRADE_DESIGN_V3_2.md` و `docs/TRADE_DESIGN_FINAL_VERIFICATION.md` ویرایش شدند. **هیچ کد، Migration، Entity، API یا جدول دیتابیس ایجاد یا تغییر نشده.** هیچ تصمیم کسب‌وکاری باز (`OD-001` تا `OD-022`) تغییر نکرد.

---

## ۱. Vessel Merge Reversal — سناریوی زنجیره‌ای

**مشکل اولیه:** سند قبلی مکانیزم Reversal یک Merge اشتباه را توصیف می‌کرد (`MergeReversed`)، اما هیچ قاعده‌ای برای حالتی که یک Merge دیگر به‌طور وابسته روی همان Target اجرا شده باشد نداشت. سناریوی دقیق: `A`→`B` ادغام می‌شود (`R1`)؛ بعداً `C`→`A` ادغام می‌شود که به‌خاطر قاعدهٔ «مسطح‌سازی در لحظهٔ اجرا»، اثر واقعی مستقیماً روی `B` نوشته می‌شود (`R2`, اثر نهایی = `C.MergedIntoVesselId = B`)؛ سپس تلاش می‌شود `R1` Reverse شود. وضعیت `C` و معتبر بودن این Reversal نامشخص بود.

**راهکار نهایی:** **Reversal یک `VesselMergeRequest` اگر هر `VesselMergeRequest` دیگری با همان `TargetVesselId` پیشنهادی (پیش از مسطح‌سازی) و `Status=Executed` وجود داشته باشد، صریحاً ممنوع است.** برای مثال بالا: چون `R2` با `TargetVesselId=A` ثبت شده، `R1` (`A`→`B`) تا وقتی `R2` خودش Reverse نشده قابل Reverse نیست. برای این بررسی، یک فیلد جدید (`AppliedTargetVesselId`) به `VesselMergeRequest` اضافه شد تا مقدار *پیشنهادی اصلی* (`TargetVesselId`، برای بررسی وابستگی) از مقدار *واقعاً اعمال‌شده پس از مسطح‌سازی* تفکیک شود.

**دلیل انتخاب:** گزینهٔ جایگزین — بازسازی/اصلاح خودکار زنجیره هنگام Reversal (مثلاً خودکار Re-flatten کردن `C` به `A` دوباره) — نیازمند تشخیص و پیمایش هم‌زمان همهٔ ادغام‌های وابسته است؛ این دقیقاً همان پیچیدگی‌ای است که قاعدهٔ یک‌گامی موجود (حداکثر یک گام عمق) عمداً از آن پرهیز می‌کند. مسدودکردن ساده (یک کوئری `WHERE TargetVesselId=@X AND Status='Executed'`، بدون Recursive Query) کم‌ریسک‌ترین گزینه است و کاربر را مجبور می‌کند وابستگی‌ها را دستی و آگاهانه، یکی‌یکی و از جدیدترین به قدیمی‌ترین، حل کند — دقیقاً طبق دستور صریح شما برای انتخاب راهکار ساده‌تر.

**محدودیت باقی‌مانده:** محدودیت قبلی («ارجاعات `Voyage.VesselId` محاسبه‌شده بین Merge و Reversal گذشته‌نگر اصلاح نمی‌شوند») بدون تغییر باقی می‌ماند؛ این قاعدهٔ جدید فقط تضمین می‌کند که **خودِ عملیات Reversal** هرگز حالت نامعتبر/میانی تولید نکند، نه اینکه گزارش‌های تاریخی بین دو Merge بازسازی شوند (که عمداً خارج از دامنهٔ این فاز است).

**ارجاع دقیق:** `docs/TRADE_DESIGN_V3_2.md` حوزهٔ ۳، بخش `VesselMergeRequest`، بند «قاعدهٔ ممنوعیت Reversal در وجود Merge وابسته» (افزوده این دور، بلافاصله بعد از بند «بازیابی/اصلاح یک Merge اشتباه»).

---

## ۲. ممنوعیت حذف فیزیکی — تثبیت سه‌لایه

**مشکل اولیه:** سند قبلی فقط گفته بود «همهٔ FKها `Restrict`»، که به‌تنهایی حذف یک رکورد **بدون** فرزند فعال را ممنوع نمی‌کند؛ هیچ کنترل صریح دیگری (نبود مسیر API حذف، کنترل سطح Persistence) مستند نشده بود.

**راهکار نهایی:** سه کنترل مستقل و مکمل، صریحاً مستند شد:
1. **نبود مسیر API/Use Case:** هیچ Endpoint/Use Case این ماژول عملیات `DELETE`/`DbSet.Remove` تعریف نمی‌کند؛ تنها نوشتن مجاز `Update` (فیلد وضعیت) یا ساخت ردیف جدید (Supersede) است.
2. **`Restrict` روی FK:** بدون تغییر — جلوگیری از حذف رکورد **با** فرزند فعال.
3. **بازبینی `ChangeTracker` در `TradeDbContext.SaveChangesAsync` (کنترل جدید، فقط توصیف‌شده):** قبل از فراخوانی پایه، بررسی `ChangeTracker.Entries<TradeEntity>().Where(e => e.State == EntityState.Deleted)` و پرتاب `InvalidOperationException` در صورت وجود — این دقیقاً حذف **بدون** فرزند (که `Restrict` آن را نمی‌بیند) را هم می‌بندد، بدون Trigger دیتابیسی یا سرویس جدید.

علاوه بر این، تفکیک `IsDeleted` (از پیش در `TradeEntity` Merged موجود — `Modules/Trade/Trade.Domain/Common/TradeEntity.cs`) از لغو کسب‌وکاری صریح شد: `IsDeleted=true` فقط برای رکورد **بدون اثر کسب‌وکاری قبلی** (مثل یک ردیف تکراری ناشی از دوبار کلیک — دقیقاً کاربرد فعلی‌اش برای `PartyRoleAssignment` در کد Merged)؛ لغو کسب‌وکاری همیشه با فیلد وضعیت دامنه‌ای (`Cancelled`/`Superseded`/`Rejected`/`Archived`) انجام می‌شود و در گزارش/تاریخچه قابل‌دیدن باقی می‌ماند.

**دلیل انتخاب:** بازبینی `ChangeTracker` در `SaveChangesAsync` یک الگوی از‌پیش‌شناخته‌شده و کم‌ریسک در همین مخزن است (`docs/SECURITY.md` همین سطح ابزار — «پاک‌سازی ChangeTracker» — را در جای دیگر مخزن از قبل به‌کار برده)؛ این دقیقاً «بدون Trigger گسترده یا راهکار پیچیده» است که خواسته شده. یک محدودیت چهارم (مثل `DENY DELETE` در سطح نقش دیتابیس SQL Server) رد شد چون نیازمند هماهنگی عملیاتی/زیرساختی خارج از دامنهٔ این ماژول است و برای این فاز توجیه ندارد.

**محدودیت باقی‌مانده:** کنترل شمارهٔ ۳ (`ChangeTracker`) فقط **توصیف** شد؛ در فاز پیاده‌سازی باید واقعاً روی `TradeDbContext.cs` نوشته و با تست Integration تأیید شود (طبق محدودیت این دور، هیچ کدی نوشته نشد).

**ارجاع دقیق:** `docs/TRADE_DESIGN_V3_2.md` قواعد مشترک، بند «Delete Behavior»، بندهای شمارهٔ ۱ تا ۴ (افزوده این دور).

---

## ۳. Audit در CostAllocation

**مشکل اولیه:** `TRADE_DESIGN_FINAL_VERIFICATION.md` برای `CostItem`/`CostAllocation` نوشته بود «Audit باید در فاز پیاده‌سازی اضافه شود» — یک شکاف صریح، بدون مشخص‌بودن مکانیزم.

**راهکار نهایی:** بدون ساخت زیرساخت Audit جدید، با همان دو الگوی از‌پیش‌موجود مخزن:
- `CostItem`/`CostAllocation` از `TradeEntity` (کلاس پایهٔ Merged، همان کلاسی که `Party`/`Product` از آن ارث می‌برند) ارث می‌برند؛ `CreatedByUserId`/`CreatedAtUtc`/`UpdatedByUserId`/`UpdatedAtUtc` بدون هیچ ستون/جدول اضافه در دسترس است.
- برای «مقدار قبل/بعد + علت تغییر»، همان الگوی Active/Superseded سه جدول Allocation گروه C (حوزه‌های ۴، ۵، ۷) روی خودِ `CostAllocation` هم اعمال شد: فیلدهای جدید `Status` (`Active`/`Superseded`/`Cancelled`)، `SupersedesAllocationId`، `ChangeReason` (اختیاری، فقط در صورت الزام Maker-Checker اجباری). اصلاح = ردیف قبلی `Superseded` می‌شود (مبلغ/مبنای قبلی دست‌نخورده می‌ماند) + ردیف جدید `Active` (مبلغ/مبنای جدید) — «قبل» و «بعد» دو ردیف واقعی‌اند، نه دو مقدار قابل‌رونویسی در یک ردیف.
- جدول نگاشت دقیق («کدام داده کجا ثبت می‌شود») در سند طراحی اضافه شد: جدول اصلی (مقادیر فعلی + Status + SupersedesAllocationId + ChangeReason)، Audit موجود (Created/Updated By/At از `TradeEntity`)، رویداد کسب‌وکاری (فقط اگر Maker-Checker روی اصلاح تسهیم لازم شود — تصمیمی که این سند **باز** می‌گذارد، نه قطعی می‌کند؛ قاعدهٔ کلی‌تر «اصلاح کنترل‌شده» از قبل در همین حوزه وجود داشت).

**دلیل انتخاب:** این دقیقاً همان الگویی است که سند از قبل سه بار (حوزه‌های ۴، ۵، ۷) استفاده کرده بود؛ تکرار آن برای `CostAllocation` یک زیرساخت موازی **نمی‌سازد**، فقط یک الگوی تثبیت‌شده را به یک حوزهٔ چهارم تعمیم می‌دهد — دقیقاً طبق دستور صریح شما («الگوی Audit موجود در فرانو را بررسی کن و همان را استفاده کن؛ زیرساخت جدید و موازی نساز»).

**محدودیت باقی‌مانده:** اینکه آیا اصلاح تسهیم هزینه نیاز به Maker-Checker دارد یا نه، یک تصمیم کسب‌وکاری **حل‌نشده** باقی می‌ماند (این سند عمداً آن را قطعی نکرد، چون جزو ۳ ابهام این دور نبود و تغییر تصمیم‌های باز موجود مجاز نبود)؛ تا آن زمان، `ChangeReason` اختیاری باقی می‌ماند.

**ارجاع دقیق:** `docs/TRADE_DESIGN_V3_2.md` حوزهٔ ۱۰، بندهای «فیلدهای اصلی» (فیلدهای جدید `Status`/`SupersedesAllocationId`/`ChangeReason`)، «وضعیت/گردش‌کار» (تفکیک دو محور)، و بند جدید «Audit و اصلاح تسهیم» (جدول نگاشت کامل).

---

## خروجی Git (واقعی، اجراشده همین لحظه)

خروجی واقعی (اجراشده همین لحظه، بدون ویرایش):

```
$ git status --short --branch
## docs/trade-handoff-status-reconciliation...origin/main [ahead 2]
 M docs/TRADE_DESIGN_RECONCILIATION.md
 M docs/TRADE_DESIGN_V3_2.md
 M docs/TRADE_OPEN_DECISIONS.md
?? docs/TRADE_DESIGN_FINAL_GAPS.md
?? docs/TRADE_DESIGN_FINAL_REVIEW.md
?? docs/TRADE_DESIGN_FINAL_VERIFICATION.md

$ git diff --stat
 docs/TRADE_DESIGN_RECONCILIATION.md |  33 ++++-
 docs/TRADE_DESIGN_V3_2.md           | 176 +++++++++++++++++++----
 docs/TRADE_OPEN_DECISIONS.md        | 279 ++++++++++++++++++++++++++----------
 3 files changed, 379 insertions(+), 109 deletions(-)

$ git diff --check
(بدون خروجی — تمیز، بدون مشکل whitespace)
```

**توضیح:** `TRADE_DESIGN_FINAL_VERIFICATION.md` و `TRADE_DESIGN_FINAL_GAPS.md` هر دو **Untracked** هستند (هیچ‌وقت commit نشده‌اند)، پس در `git diff --stat` (که فقط فایل‌های Tracked را نشان می‌دهد) ظاهر نمی‌شوند؛ ویرایش‌های این دور روی `TRADE_DESIGN_FINAL_VERIFICATION.md` در همان فایل اعمال شده‌اند، فقط `git diff` آن‌ها را نشان نمی‌دهد چون پیش از این commit نشده بود. `docs/TRADE_OPEN_DECISIONS.md` در این دور ویرایش نشد؛ `M` آن از دور قبل باقی مانده.

**فایل‌های Untracked:** `docs/TRADE_DESIGN_FINAL_REVIEW.md`، `docs/TRADE_DESIGN_FINAL_VERIFICATION.md`، `docs/TRADE_DESIGN_FINAL_GAPS.md` (همین سند).

**Commit/Push:** هیچ Commit یا Push در این دور انجام نشد؛ طبق دستور صریح شما منتظر درخواست شما می‌مانم.
