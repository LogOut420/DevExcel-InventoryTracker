# Inventory Tracker (.NET 10 MVC)

A self-contained Inventory & Stock Movements module built with **ASP.NET Core 10 (MVC)**, **Entity Framework Core**, **SQL Server LocalDB**, and **xUnit**.

- **Why Soft Deactivate (`IsActive`) Over Hard Delete:**
  Systems mein agar kisi product ko hard delete kar diya jaye toh uski historical `StockMovements` 
  audit trail aur past stock reports corrupt ho jati hain. 

  Isliye database level par `DeleteBehavior.Restrict` lagaya gaya hai aur UI mein **Soft Deactivate (`IsActive = false`)** 
  provide kiya gaya ta k deactivated products catalog mein archived rehte hain lekin un par koi new `In` ya `Out` movement 
  record nahi ho saky.

---

## 1. How to Run Locally (Commands, DB Setup & Seed Data)

Is project ko bilkul minimal setup ke saath design kiya gaya hai taake aapko koi manual SQL script na chalani pary:

### Prerequisites
- .NET 10 SDK
- SQL Server LocalDB (Visual Studio ke saath pre-installed hota hai) ya koi bhi SQL Server instance

### Step-by-Step Commands
1. Clone the repository:
   
   git clone https://github.com/LogOut420/DevExcel-InventoryTracker.git
cd DevExcel-InventoryTracker

