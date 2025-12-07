namespace Hospitality.Models;

public class Payment
{
    public int payment_id { get; set; }
    public int booking_id { get; set; }
    public decimal amount { get; set; }
    public string payment_method { get; set; } = ""; // card, gcash, grab_pay
    public string payment_status { get; set; } = "pending"; // pending, completed, failed, refunded
    public string? payment_intent_id { get; set; } // PayMongo reference
    public string? checkout_session_id { get; set; }
    public DateTime payment_date { get; set; }
    public string payment_type { get; set; } = "full"; // full, downpayment, partial, balance
    public string? notes { get; set; }

    // PascalCase aliases
    public int PaymentId => payment_id;
    public int BookingId => booking_id;
    public decimal Amount => amount;
    public string PaymentMethod => payment_method;
    public string PaymentStatus => payment_status;
    public DateTime PaymentDate => payment_date;
    public string PaymentType => payment_type;
}

public class BookingPaymentSummary
{
    public int booking_id { get; set; }
    public decimal total_amount { get; set; }
    public decimal total_paid { get; set; }
    public decimal remaining_balance { get; set; }
    public bool is_fully_paid { get; set; }
    public List<Payment> payments { get; set; } = new();
 
    public decimal TotalAmount => total_amount;
    public decimal TotalPaid => total_paid;
    public decimal RemainingBalance => remaining_balance;
    public bool IsFullyPaid => is_fully_paid;
}

public class PendingBooking
{
  public int? temp_booking_id { get; set; }
    public int client_id { get; set; }
    public DateTime check_in_date { get; set; }
    public DateTime check_out_date { get; set; }
    public int person_count { get; set; }
    public string? client_request { get; set; }
    public TimeOnly check_in_time { get; set; }
    public TimeOnly check_out_time { get; set; }
    public List<int> room_ids { get; set; } = new();
    public string? rate_preference { get; set; }
    public string? special_requests { get; set; }
    
    // Loyalty-based pricing
    public string loyalty_tier { get; set; } = "Bronze";
    public decimal discount_percentage { get; set; }
    public decimal subtotal { get; set; }
    public decimal discount_amount { get; set; }
    public decimal taxes_and_fees { get; set; }
    public decimal total_amount { get; set; }
}
