# AI Collaboration Notes


## Tools Used
- **Gemini (LLM)** for architecture review, best practices, and performance techniques.


## How AI Was Used
- **Scaffolding & UI Layout:** Initial ASP.NET Core 10 MVC boilerplate, EF Core `AppDbContext` fluent configurations, aur clean Bootstrap 
- Razor views (`Index`, `Details`, `Create/Edit`) ko tezi se structure karne ke liye.
- **LINQ & SQL Projection:** `SUM(In) - SUM(Out)` ke liye single-query `.Select()` projections likhne ke liye.
- **Test Setup:** Isolated SQLite In-Memory xUnit fixtures setup karne ke liye jo derived stock calculation aur non-negative stock rule ko verify karte hain.


## Examples Where AI Got It Wrong & How I Caught/Fixed Them

### 1. Dashboard Summary Cards Calculated Only From the Current Page
- **What AI Got Wrong:** Jab server-side pagination (5 items per page) ke saath top summary cards (`Total Active SKUs`, `Active Units on Hand`, `Below Reorder Level`) implement 
kiye gaye, toh initial AI-generated code mein ye counts poori database ki bajaye sirf current page ke `Model.Items` se calculate ho rahe thay.
- **How I Caught & Fixed It:** Jab maine Page 2 par low-stock items test kiye toh top dashboard cards ke numbers active page ke mutabiq change hone lage aur ghalat count dikhane lage. 
Maine summary metrics ko aik alag `GetDashboardStatsAsync()` query mein shift kiya taake cards hamesha poori database ka accurate global count dikhayein.

### 2. Case-Sensitive Duplicate SKU Check
- **What AI Got Wrong:** Initial SKU uniqueness check mein AI ne simple `p.Sku == dto.Sku` comparison likha tha, jis ki wajah se `"prd-1001"` aur `"PRD-1001"` jaise case variants 
alag-alag SKUs ban kar database mein save ho sakte thay.
- **How I Caught & Fixed It:** Lowercase SKUs ke saath duplicate-entry test karte waqt maine dekha ke inconsistent casing validation ko bypass kar rahi thi. 
Maine service layer mein SKU inputs ko `.Trim().ToUpperInvariant()` se normalize kiya.

