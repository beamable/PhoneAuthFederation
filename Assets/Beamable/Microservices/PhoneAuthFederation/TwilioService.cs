using Beamable.Common;
using Beamable.Server;
using Beamable.Server.Api.RealmConfig;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Unity.Plastic.Newtonsoft.Json;

namespace Beamable.Microservices.Twilio
{
    public class TwilioService
    {
        private readonly IMicroserviceRealmConfigService _realmConfigService;
        private RealmConfig _settings;

        private string _verifyURL;
        private string _apiURL;
        private readonly HttpClient _httpClient;

        private string _messagingSID;

        public TwilioService(IMicroserviceRealmConfigService realmConfigService)
        {
            _realmConfigService = realmConfigService;
            _httpClient = new HttpClient();
        }

        public async Promise Init()
        {
            _settings = await _realmConfigService.GetRealmConfigSettings();

            var accountSID = _settings.GetSetting("twilio", "account_sid");
            var authToken = _settings.GetSetting("twilio", "auth_token");
            var verifySID = _settings.GetSetting("twilio", "verify_sid");
            _messagingSID = _settings.GetSetting("twilio", "messaging_sid");

            var byteArray = Encoding.ASCII.GetBytes($"{accountSID}:{authToken}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            _verifyURL = $"https://verify.twilio.com/v2/Services/{verifySID}";
            _apiURL = $"https://api.twilio.com/2010-04-01/Accounts/{accountSID}";
        }

        public async Task<TwilioMessageResponse> SendMessage(string phoneNumber, string body)
        {
            var request = await _httpClient.PostAsync($"{_apiURL}/Messages.json", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "MessagingServiceSid", _messagingSID},
                { "To", phoneNumber },
                { "Body", body }
            }));

            var responseBody = await request.Content.ReadAsStringAsync();
            if (request.IsSuccessStatusCode)
            {
                var responseDeserialized = JsonConvert.DeserializeObject<TwilioMessageResponse>(responseBody);
                return responseDeserialized;
            }
            else
            {
                var responseDeserialized = JsonConvert.DeserializeObject<TwilioErrorResponse>(responseBody);
                BeamableLogger.LogWarning(responseBody);
                throw new MicroserviceException(500, "MessagingError", "An error occurred when sending the message.");
            }
        }

        public async Task<TwilioVerificationResponse> StartNewVerification(string phoneNumber, string channel)
        {
            var request = await _httpClient.PostAsync($"{_verifyURL}/Verifications", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "To", phoneNumber },
                { "Channel", channel }
            }));
            var response = await ParseVerifyResponse(request);
            return response;
        }

        public async Task<bool> VerificationCheck(string phoneNumber, string code)
        {
            var request = await _httpClient.PostAsync($"{_verifyURL}/VerificationCheck", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "To", phoneNumber },
                { "Code", code }
            }));

            var response = await ParseVerifyResponse(request);
            return response.IsApproved;
        }

        private async Task<TwilioVerificationResponse> ParseVerifyResponse(HttpResponseMessage request)
        {
            var responseBody = await request.Content.ReadAsStringAsync();
            if (request.IsSuccessStatusCode)
            {
                var responseDeserialized = JsonConvert.DeserializeObject<TwilioVerificationResponse>(responseBody);
                return responseDeserialized;
            }
            else
            {
                var responseDeserialized = JsonConvert.DeserializeObject<TwilioErrorResponse>(responseBody);
                if (responseDeserialized.IsMaxAttemptsError)
                {
                    throw new MicroserviceException(400, "MaxAttemptsReached", "Max check attempts reached. Please wait 10 minutes before trying again.");
                }

                BeamableLogger.LogWarning(responseBody);
                throw new MicroserviceException(500, "VerificationError", "An error occurred when verifying the code.");
            }
        }

        // Supported Twilio channels are sms, call, email, and whatsapp.
        public bool IsValidChannel(string channel)
        {
            switch (channel)
            {
                case "sms":
                    return true;
                case "call":
                    return true;
                case "whatsapp":
                    return true;
                default:
                    return false;
            }
        }

        //TODO: Validate phone number is E.164 standard
        public bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return false;

            return true;
        }
    }

    public class TwilioMessageResponse
    {
        public string status;
    }

    public class TwilioVerificationResponse
    {
        public string status;

        public bool IsApproved => status == "approved";
        public bool IsPending => status == "pending";
    }

    public class TwilioErrorResponse
    {
        public int code;
        public int status;
        public string message;

        public bool IsMaxAttemptsError => code == 60202;
    }
}