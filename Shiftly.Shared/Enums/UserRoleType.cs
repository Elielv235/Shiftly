using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftly.Shared.Enums
{
    // ארבעת סוגי המשתמשים שהוגדרו בייזום Shiftly
    public enum UserRoleType
    {
        Guest = 1,        // אורח - צפייה בדמו בלבד
        Worker = 2,       // עובד רגיל
        ShiftManager = 3, // מנהל משמרת
        HRAdmin = 4       // מנהל כוח אדם / אדמין
    }
}
