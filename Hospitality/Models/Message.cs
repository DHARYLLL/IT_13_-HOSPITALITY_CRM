namespace Hospitality.Models
{
  public class Message
    {
        public int message_id { get; set; }
        public int client_id { get; set; }
        public string? message_subject { get; set; }
        public string? message_body { get; set; }
        public string? message_type { get; set; } // "offer", "service", "billing", "general", "outgoing"
        public bool is_read { get; set; }
        public DateTime sent_date { get; set; }
        public int? booking_id { get; set; } // Optional: link to a booking
        public string? action_label { get; set; } // e.g., "Book a stay", "See eligible dates"
        public string? action_url { get; set; } // URL for the action button
        public string? regarding_text { get; set; } // e.g., "Regarding: Upcoming stay, billing, or general question"
    
      // Navigation properties
    public string? client_name { get; set; }
     public string? booking_reference { get; set; }
    }
    
    public class MessageFilter
    {
        public string FilterType { get; set; } = "All"; // All, Unread, Stays, Offers
   public string TimeRange { get; set; } = "Last 90 days"; // Last 90 days, Last 30 days, etc.
  }
    
    public class EmailRequest
    {
        public int client_id { get; set; }
        public string? regarding { get; set; } // "Front desk (HarborKey Waterfront)", etc.
   public string? subject { get; set; }
        public string? message_body { get; set; }
      public string? email_to { get; set; } // hotel team email
    }
}
