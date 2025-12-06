using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Hospitality.Services
{
    public class PayMongoService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;
        private readonly string _publicKey;

        public PayMongoService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            // Read keys from configuration with better error handling
            _secretKey = configuration["PayMongo:SecretKey"] ?? "";
            _publicKey = configuration["PayMongo:PublicKey"] ?? "";

            if (string.IsNullOrEmpty(_secretKey))
            {
                Console.WriteLine("?? WARNING: PayMongo SecretKey not configured. Payment features will not work.");
                Console.WriteLine("?? TIP: Make sure appsettings.json is included as EmbeddedResource in the project file.");
                // Don't throw exception - allow app to run but payment features won't work
                return;
            }

            if (string.IsNullOrEmpty(_publicKey))
            {
                Console.WriteLine("?? WARNING: PayMongo PublicKey not configured.");
                return;
            }

            // Set base address for PayMongo API
            _httpClient.BaseAddress = new Uri("https://api.paymongo.com/v1/");

            // Set authorization header with secret key
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_secretKey}:"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            Console.WriteLine("? PayMongo service initialized successfully");
        }

        /// <summary>
        /// Creates a PayMongo Checkout Session - This redirects to PayMongo's hosted payment page
        /// which handles all payment methods (Card, GCash, GrabPay, etc.)
        /// </summary>
        public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request)
        {
            try
            {
                Console.WriteLine("=== Creating PayMongo Checkout Session ===");
                Console.WriteLine($"Amount: {request.Amount} centavos");
                Console.WriteLine($"Description: {request.Description}");
                Console.WriteLine($"Success URL: {request.SuccessUrl}");
                Console.WriteLine($"Cancel URL: {request.CancelUrl}");

                var lineItems = new[]
                {
                    new
                    {
                        name = request.LineItemName,
                        description = request.LineItemDescription,
                        amount = request.Amount,
                        currency = request.Currency,
                        quantity = 1
                    }
                };

                var payload = new
                {
                    data = new
                    {
                        attributes = new
                        {
                            billing = request.Billing != null ? new
                            {
                                name = request.Billing.Name,
                                email = request.Billing.Email,
                                phone = request.Billing.Phone
                            } : null,
                            send_email_receipt = true,
                            show_description = true,
                            show_line_items = true,
                            description = request.Description,
                            line_items = lineItems,
                            payment_method_types = request.PaymentMethodTypes,
                            success_url = request.SuccessUrl,
                            cancel_url = request.CancelUrl,
                            reference_number = request.ReferenceNumber,
                            metadata = request.Metadata
                        }
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(payload, jsonOptions);
                Console.WriteLine($"Request Payload: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("checkout_sessions", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response Status: {response.StatusCode}");
                Console.WriteLine($"Response Body: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var root = jsonDoc.RootElement;

                    string checkoutId = "";
                    string checkoutUrl = "";
                    string status = "";

                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("id", out var idElement))
                        {
                            checkoutId = idElement.GetString() ?? "";
                        }

                        if (dataElement.TryGetProperty("attributes", out var attrElement))
                        {
                            if (attrElement.TryGetProperty("checkout_url", out var urlElement))
                            {
                                checkoutUrl = urlElement.GetString() ?? "";
                            }
                            if (attrElement.TryGetProperty("status", out var statusElement))
                            {
                                status = statusElement.GetString() ?? "";
                            }
                         }
                     }

                    Console.WriteLine($"? Checkout Session Created!");
                    Console.WriteLine($"   ID: {checkoutId}");
                    Console.WriteLine($"   URL: {checkoutUrl}");
                    Console.WriteLine($"   Status: {status}");

                    return new CheckoutSessionResult
                    {
                        Success = true,
                        CheckoutSessionId = checkoutId,
                        CheckoutUrl = checkoutUrl,
                        Status = status
                    };
                }
                else
                {
                    Console.WriteLine($"? API Error: {responseContent}");
                    return new CheckoutSessionResult
                    {
                        Success = false,
                        ErrorMessage = $"PayMongo API Error: {responseContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Exception: {ex.Message}");
                return new CheckoutSessionResult
                {
                    Success = false,
                    ErrorMessage = $"Checkout session error: {ex.Message}"
                };
            }
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

        // Create Source for e-wallet payments (GCash, PayMaya, GrabPay)
        public async Task<SourceResult> CreateSourceAsync(CreateSourceRequest request)
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
                            type = request.Type,
                            redirect = new
                            {
                                success = request.RedirectSuccessUrl,
                                failed = request.RedirectFailedUrl
                            },
                            billing = request.Billing,
                            metadata = request.Metadata
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("sources", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SourceApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    return new SourceResult
                    {
                        Success = true,
                        SourceId = result?.Data?.Id ?? "",
                        CheckoutUrl = result?.Data?.Attributes?.Redirect?.CheckoutUrl ?? "",
                        Status = result?.Data?.Attributes?.Status ?? ""
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new SourceResult
                    {
                        Success = false,
                        ErrorMessage = $"PayMongo API Error: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new SourceResult
                {
                    Success = false,
                    ErrorMessage = $"Source creation error: {ex.Message}"
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

    // ============================================
    // CHECKOUT SESSION MODELS (NEW)
    // ============================================

    public class CreateCheckoutSessionRequest
    {
        public int Amount { get; set; }
        public string Currency { get; set; } = "PHP";
        public string Description { get; set; } = "";
        public string LineItemName { get; set; } = "";
        public string LineItemDescription { get; set; } = "";
        public string[] PaymentMethodTypes { get; set; } = { "card", "gcash", "grab_pay" };
        public string SuccessUrl { get; set; } = "";
        public string CancelUrl { get; set; } = "";
        public string ReferenceNumber { get; set; } = "";
        public CheckoutBilling? Billing { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class CheckoutBilling
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    public class CheckoutSessionResult
    {
        public bool Success { get; set; }
        public string CheckoutSessionId { get; set; } = "";
        public string CheckoutUrl { get; set; } = "";
        public string Status { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    // ============================================
    // EXISTING MODELS
    // ============================================

    public class CreatePaymentIntentRequest
    {
        public int Amount { get; set; }
        public string Currency { get; set; } = "PHP";
        public string[] PaymentMethodsAllowed { get; set; } = { "card", "gcash", "paymaya" };
        public string Description { get; set; } = "";
        public string StatementDescriptor { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class CreateSourceRequest
    {
        public int Amount { get; set; }
        public string Currency { get; set; } = "PHP";
        public string Type { get; set; } = "gcash";
        public string RedirectSuccessUrl { get; set; } = "";
        public string RedirectFailedUrl { get; set; } = "";
        public SourceBilling? Billing { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class SourceBilling
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
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

    public class SourceResult
    {
        public bool Success { get; set; }
        public string SourceId { get; set; } = "";
        public string CheckoutUrl { get; set; } = "";
        public string Status { get; set; } = "";
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

    // Source API Response Models
    public class SourceApiResponse
    {
        public SourceData? Data { get; set; }
    }

    public class SourceData
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public SourceAttributes? Attributes { get; set; }
    }

    public class SourceAttributes
    {
        public string Status { get; set; } = "";
        public RedirectInfo? Redirect { get; set; }
    }

    public class RedirectInfo
    {
        public string CheckoutUrl { get; set; } = "";
        public string Success { get; set; } = "";
        public string Failed { get; set; } = "";
    }
}