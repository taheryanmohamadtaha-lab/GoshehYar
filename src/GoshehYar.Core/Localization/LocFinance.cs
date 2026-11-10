namespace GoshehYar.Core.Localization;

/// <summary>Texts of the date converter and bank/finance tools (Persian, English). Merged into <see cref="Loc"/>. Keys must be unique across all tables.</summary>
internal static class LocFinance
{
    internal static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["calc_tab_dateconv"] = ("تبدیل تاریخ", "Date converter"),
        ["calc_tab_bank"] = ("سود و بانک", "Interest & bank"),

        ["dc_j2g_title"] = ("شمسی ← میلادی", "Jalali → Gregorian"),
        ["dc_g2j_title"] = ("میلادی ← شمسی", "Gregorian → Jalali"),
        ["dc_year"] = ("سال", "Year"),
        ["dc_month"] = ("ماه", "Month"),
        ["dc_day"] = ("روز", "Day"),
        ["dc_invalid"] = ("تاریخ معتبر نیست.", "The date is not valid."),
        ["dc_result_j2g"] = ("میلادی: {0} ({1})", "Gregorian: {0} ({1})"),
        ["dc_result_g2j"] = ("شمسی: {0} ({1})", "Jalali: {0} ({1})"),
        ["dc_year_title"] = ("فقط سال", "Year only"),
        ["dc_year_j_label"] = ("سال شمسی", "Jalali year"),
        ["dc_year_g_label"] = ("سال میلادی", "Gregorian year"),
        ["dc_year_j2g"] = ("سال شمسی {0}: از {1} تا {2} میلادی{3}", "Jalali year {0}: {1} to {2} (Gregorian){3}"),
        ["dc_year_g2j"] = ("سال میلادی {0}: از {1} تا {2} شمسی", "Gregorian year {0}: {1} to {2} (Jalali)"),
        ["dc_leap"] = (" — سال کبیسه", " — leap year"),
        ["dc_hint"] = ("خانه‌ها را با عدد فارسی یا انگلیسی پر کنید. نتیجه خودکار محاسبه می‌شود.", "Type numbers in Persian or English digits. The result updates automatically."),

        ["fin_hint"] = ("نتیجه‌ها تقریبی و برای برنامه‌ریزی‌اند؛ شرایط واقعی هر بانک (روش محاسبه، مالیات، کارمزد) ممکن است فرق کند.", "Results are estimates for planning; each bank's real rules (method, tax, fees) may differ."),
        ["fin_basis"] = ("مبنای سال", "Year basis"),
        ["fin_basis_365"] = ("۳۶۵ روز", "365 days"),
        ["fin_basis_360"] = ("۳۶۰ روز", "360 days"),
        ["fin_principal"] = ("مبلغ سپرده / اصل پول", "Deposit / principal"),
        ["fin_rate"] = ("سود سالانه (٪)", "Annual rate (%)"),
        ["fin_days"] = ("مدت (روز)", "Period (days)"),
        ["fin_years"] = ("مدت (سال)", "Period (years)"),
        ["fin_months"] = ("مدت (ماه)", "Period (months)"),

        ["fin_simple_title"] = ("سود ساده سپرده", "Simple deposit interest"),
        ["fin_simple_profit"] = ("سود دوره: {0}", "Interest for the period: {0}"),
        ["fin_simple_total"] = ("مبلغ نهایی: {0}", "Final amount: {0}"),
        ["fin_simple_day"] = ("سود هر روز: {0}", "Interest per day: {0}"),
        ["fin_simple_month"] = ("سود هر ماه (تقریبی): {0}", "Interest per month (approx.): {0}"),
        ["fin_simple_year"] = ("سود یک سال: {0}", "Interest per year: {0}"),

        ["fin_compound_title"] = ("سود مرکب", "Compound interest"),
        ["fin_compound_freq"] = ("تعداد دفعات سودگیری در سال", "Compounding per year"),
        ["fin_freq_1"] = ("سالانه", "Yearly"),
        ["fin_freq_4"] = ("فصلی", "Quarterly"),
        ["fin_freq_12"] = ("ماهانه", "Monthly"),
        ["fin_freq_365"] = ("روزانه", "Daily"),
        ["fin_compound_total"] = ("مبلغ نهایی: {0}", "Final amount: {0}"),
        ["fin_compound_profit"] = ("سود کل: {0}", "Total interest: {0}"),
        ["fin_compound_effective"] = ("نرخ واقعی سالانه: {0}٪", "Effective annual rate: {0}%"),

        ["fin_rates_title"] = ("تبدیل درصد سود (روزانه، ماهانه، سالانه)", "Rate converter (daily, monthly, yearly)"),
        ["fin_rates_from_annual"] = ("از سالانه: روزانه {0}٪ — ماهانه {1}٪", "From yearly: daily {0}% — monthly {1}%"),
        ["fin_rates_daily_label"] = ("درصد روزانه", "Daily %"),
        ["fin_rates_monthly_label"] = ("درصد ماهانه", "Monthly %"),
        ["fin_rates_from_daily"] = ("از روزانه: سالانه {0}٪", "From daily: yearly {0}%"),
        ["fin_rates_from_monthly"] = ("از ماهانه: سالانه {0}٪", "From monthly: yearly {0}%"),

        ["fin_savings_title"] = ("پس‌انداز ماهانه", "Monthly savings"),
        ["fin_savings_initial"] = ("موجودی اولیه", "Starting amount"),
        ["fin_savings_deposit"] = ("واریز هر ماه", "Deposit each month"),
        ["fin_savings_total"] = ("موجودی نهایی: {0}", "Final balance: {0}"),
        ["fin_savings_paid"] = ("جمع واریزی‌ها: {0}", "Total paid in: {0}"),
        ["fin_savings_profit"] = ("سود به دست‌آمده: {0}", "Interest earned: {0}"),

        ["fin_loan_title"] = ("قسط وام", "Loan installment"),
        ["fin_loan_amount"] = ("مبلغ وام", "Loan amount"),
        ["fin_loan_rate"] = ("نرخ سود سالانه وام (٪)", "Loan annual rate (%)"),
        ["fin_loan_payment"] = ("قسط ماهانه: {0}", "Monthly installment: {0}"),
        ["fin_loan_total"] = ("جمع بازپرداخت: {0}", "Total repayment: {0}"),
        ["fin_loan_interest"] = ("جمع سود وام: {0}", "Total loan interest: {0}"),
    };
}
