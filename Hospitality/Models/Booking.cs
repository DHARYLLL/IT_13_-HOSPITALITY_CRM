namespace Hospitality.Models;

public class Booking
{
    public int booking_id { get; set; }
    public int client_id { get; set; }
    public int room_id { get; set; }
    public DateTime booking_date { get; set; }
    public DateTime check_in_date { get; set; }
    public DateTime check_out_date { get; set; }
    public int guest_count { get; set; }
    public string bed_type { get; set; } = string.Empty;
 public string booking_status { get; set; } = "Confirmed";
    
 // Related data
    public string? room_name { get; set; }
    public int? room_number { get; set; }
    public int? room_floor { get; set; }
    public decimal? room_price { get; set; }
    public string? room_facilities { get; set; }

    // PascalCase aliases for convenience
    public int BookingId => booking_id;
 public int ClientId => client_id;
    public int RoomId => room_id;
    public DateTime BookingDate => booking_date;
    public DateTime CheckInDate => check_in_date;
    public DateTime CheckOutDate => check_out_date;
    public int GuestCount => guest_count;
    public string BedType => bed_type;
    public string BookingStatus => booking_status;
    public string RoomName => room_name ?? string.Empty;
    public int RoomNumber => room_number ?? 0;
    public int RoomFloor => room_floor ?? 0;
    public decimal RoomPrice => room_price ?? 0;
    public string RoomFacilities => room_facilities ?? string.Empty;
}
