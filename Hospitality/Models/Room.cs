namespace Hospitality.Models;

// Room model aligned to database schema columns
public class Room
{
 public int room_id { get; set; }
 public string room_name { get; set; } = string.Empty;
 public int room_number { get; set; }
 public int room_floor { get; set; }
 public decimal room_price { get; set; }
 //public string room_available { get; set; } = "Yes"; // e.g., Yes / No
 public string room_status { get; set; } = "Available";

 // Optional fields
 public byte[]? room_picture { get; set; } // varbinary(max)
 public string? room_amenities { get; set; } // comma-separated or JSON

 // Read-only aliases for UI convenience
 public int Id => room_id;
 public string Name => room_name;
 public int Number => room_number;
 public int Floor => room_floor;
 public decimal Price => room_price;
 //public string Available => room_available;
 public string Status => room_status;
}
