using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hospitality.Services
{
    public class PayMongoService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;
        private readonly string _publicKey;

        public PayMongoService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // In a real application, these should come from secure configuration
            _secretKey = "sk_test_your_secret_key_here"; // Replace with your actual secret key
            _publicKey = "pk_test_your_public_key_here"; // Replace with your actual public key

            // Set base address for PayMongo API
            _httpClient.BaseAddress = new Uri("https://api.paymongo.com/v1/");

            // Set authorization header with secret key
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_secretKey}:"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }

        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request)
        {
            try
            {
                var payload = new
                {
                    data = new
                    {
                        attributes = new
                        {
                            amount = request.Amount,
                            currency = request.Currency,
                            payment_method_allowed = request.PaymentMethodsAllowed,
                            payment_method_options = new
                            {
                                card = new
                                {
                                    request_three_d_secure = "automatic"
                                }
                            },
                            description = request.Description,
                            statement_descriptor = request.StatementDescriptor,
                            metadata = request.Metadata
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("payment_intents", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PayMongoApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return new PaymentIntentResult
                    {
                        Success = true,
                        PaymentIntentId = result?.Data?.Id ?? "",
                        ClientSecret = result?.Data?.Attributes?.ClientKey ?? "",
                        Status = result?.Data?.Attributes?.Status ?? "",
                        NextAction = result?.Data?.Attributes?.NextAction
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new PaymentIntentResult
                    {
                        Success = false,
                        ErrorMessage = $"PayMongo API Error: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new PaymentIntentResult
                {
                    Success = false,
                    ErrorMessage = $"Payment processing error: {ex.Message}"
                };
            }
        }

        public async Task<PaymentResult> AttachPaymentMethodAsync(string paymentIntentId, string paymentMethodId)
        {
            try
            {
                var payload = new
                {
                    data = new
                    {
                        attributes = new
                        {
                            payment_method = paymentMethodId,
                            client_key = _publicKey
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"payment_intents/{paymentIntentId}/attach", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PayMongoApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return new PaymentResult
                    {
                        Success = true,
                        PaymentIntentId = result?.Data?.Id ?? "",
                        Status = result?.Data?.Attributes?.Status ?? "",
                        NextAction = result?.Data?.Attributes?.NextAction
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new PaymentResult
                    {
                        Success = false,
                        ErrorMessage = $"PayMongo API Error: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = $"Payment attachment error: {ex.Message}"
                };
            }
        }

        public async Task<PaymentMethodResult> CreatePaymentMethodAsync(CreatePaymentMethodRequest request)
        {
            try
            {
                object payload;

                if (request.Type == "card")
                {
                    payload = new
                    {
                        data = new
                        {
                            attributes = new
                            {
                                type = "card",
                                details = new
                                {
                                    card_number = request.CardDetails?.Number,
                                    exp_month = request.CardDetails?.ExpMonth,
                                    exp_year = request.CardDetails?.ExpYear,
                                    cvc = request.CardDetails?.Cvc
                                },
                                billing = request.BillingDetails
                            }
                        }
                    };
                }
                else
                {
                    payload = new
                    {
                        data = new
                        {
                            attributes = new
                            {
                                type = request.Type,
                                billing = request.BillingDetails
                            }
                        }
                    };
                }

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("payment_methods", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PayMongoApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return new PaymentMethodResult
                    {
                        Success = true,
                        PaymentMethodId = result?.Data?.Id ?? "",
                        Type = result?.Data?.Attributes?.Type ?? ""
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new PaymentMethodResult
                    {
                        Success = false,
                        ErrorMessage = $"PayMongo API Error: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new PaymentMethodResult
                {
                    Success = false,
                    ErrorMessage = $"Payment method creation error: {ex.Message}"
                };
            }
        }
    }

    // Request/Response Models
    public class CreatePaymentIntentRequest
    {
        public int Amount { get; set; }
        public string Currency { get; set; } = "PHP";
        public string[] PaymentMethodsAllowed { get; set; } = { "card", "gcash", "paymaya" };
        public string Description { get; set; } = "";
        public string StatementDescriptor { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CreatePaymentMethodRequest
    {
        public string Type { get; set; } = "";
        public CardDetails? CardDetails { get; set; }
        public BillingDetails? BillingDetails { get; set; }
    }

    public class CardDetails
    {
        public string Number { get; set; } = "";
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvc { get; set; } = "";
    }

    public class BillingDetails
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public Address? Address { get; set; }
    }

    public class Address
    {
        public string Line1 { get; set; } = "";
        public string Line2 { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string Country { get; set; } = "PH";
    }

    public class PaymentIntentResult
    {
        public bool Success { get; set; }
        public string PaymentIntentId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string Status { get; set; } = "";
        public object? NextAction { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string PaymentIntentId { get; set; } = "";
        public string Status { get; set; } = "";
        public object? NextAction { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public class PaymentMethodResult
    {
        public bool Success { get; set; }
        public string PaymentMethodId { get; set; } = "";
        public string Type { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    // PayMongo API Response Models
    public class PayMongoApiResponse
    {
        public PayMongoData? Data { get; set; }
    }

    public class PayMongoData
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public PayMongoAttributes? Attributes { get; set; }
    }

    public class PayMongoAttributes
    {
        public string Status { get; set; } = "";
        public string ClientKey { get; set; } = "";
        public string Type { get; set; } = "";
        public object? NextAction { get; set; }
    }
}