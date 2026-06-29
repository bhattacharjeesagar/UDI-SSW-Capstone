namespace UDI_SSW_Prototype.Models
{
    public class UserMaster
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AssociatedDid { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
