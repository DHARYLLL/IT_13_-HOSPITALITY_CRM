namespace Hospitality.Models;

public class User
{
    public int userId { get; set; }
    public int UserId => userId;
    public int role_id { get; set; }
    public int RoleId => role_id;
    public string? user_fname { get; set; }
    public string? FirstName => user_fname;
    public string? user_mname { get; set; } 
    public string? MiddleName => user_mname;
    public string? user_lname { get; set; }
    public string? LastName => user_lname;
    public DateTime? user_brith_date { get; set; }
    public DateTime? BirthDate => user_brith_date;
    public string? user_email { get; set; }
    public string? Email => user_email;
    public string? user_contact_number { get; set; }
    public string? ContactNumber => user_contact_number;
    public string? user_password { get; set; } // hashed password recommended
    public string? Password => user_password;
    public string? roleName { get; set; }
    public string? RoleName => roleName;
}

public static class UserRoles
{
    public const string Admin = "admin";
    public const string Staff = "staff";
    public const string Client = "client";
}
