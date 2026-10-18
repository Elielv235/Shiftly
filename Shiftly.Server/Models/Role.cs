using System.Collections.Generic;

namespace Shiftly.Server.Models
{
    /// <summary>
    /// ישות תפקיד התואמת לטבלת Roles במסד הנתונים
    /// </summary>
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }

        // קשר של אחד-לרבים: לתפקיד יכולים להיות מספר משתמשים
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
