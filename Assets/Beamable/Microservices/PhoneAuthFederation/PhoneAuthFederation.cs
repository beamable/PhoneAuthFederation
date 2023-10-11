using System.Net;
using System.Threading.Tasks;
using Beamable.Common;
using Beamable.Microservices.Twilio;
using Beamable.Server;

namespace Beamable.Microservices
{
    [Microservice("PhoneAuthFederation")]
    public class PhoneAuthFederation : Microservice, IFederatedLogin<PhoneNumberIdentity>
    {
        [ConfigureServices]
        public static void Configure(IServiceBuilder builder)
        {
            builder.Builder.AddSingleton<TwilioService>();
        }

        [InitializeServices]
        public static async Task Init(IServiceInitializer init)
        {
            var config = init.GetService<TwilioService>();
            await config.Init();
        }

        public async Promise<FederatedAuthenticationResponse> Authenticate(string token, string challenge, string solution)
        {
            var phoneNumber = token;
            var smsCode = solution;
            var channel = "sms";

            // Skip the real auth flow 
            if (IsTestPhoneNumber(phoneNumber))
            {
                return TestPhoneNumber(phoneNumber, smsCode, channel);
            }

            var twilio = Provider.GetService<TwilioService>();
            if (!twilio.IsValidPhoneNumber(phoneNumber))
            {
                throw new InvalidAuthenticationRequest("Phone number is invalid.");
            }

            // Start a new verification
            if (string.IsNullOrEmpty(smsCode))
            {
                if (!twilio.IsValidChannel(channel))
                    throw new InvalidAuthenticationRequest("Channel is invalid.");

                await twilio.StartNewVerification(phoneNumber, channel);
                return new FederatedAuthenticationResponse
                {
                    challenge = $"A code has been sent to {phoneNumber} by {channel}.",
                    challenge_ttl = 600
                };
            }

            // Attempt to verify SMS Code
            var isApproved = await twilio.VerificationCheck(phoneNumber, smsCode);
            if (!isApproved)
            {
                throw new InvalidAuthenticationRequest("Phone number has not been verified.");
            }

            return new FederatedAuthenticationResponse
            {
                user_id = phoneNumber
            };
        }

        [Callable]
        public async Promise SendMessage(string phoneNumber, string body)
        {
            var twilio = Provider.GetService<TwilioService>();
            await twilio.SendMessage(phoneNumber, body);
        }

        private bool IsTestPhoneNumber(string phoneNumber)
        {
            return phoneNumber.Contains("+1000");
        }

        private FederatedAuthenticationResponse TestPhoneNumber(string phoneNumber, string smsCode, string channel)
        {
            if (string.IsNullOrEmpty(smsCode))
            {
                return new FederatedAuthenticationResponse
                {
                    challenge = $"A code has been sent to {phoneNumber} by {channel}.",
                    challenge_ttl = 600
                };
            }
            else if (smsCode == "7337")
            {
                return new FederatedAuthenticationResponse
                {
                    user_id = phoneNumber
                };
            }

            throw new MicroserviceException(400, "InvalidPhoneNumber", $"Invalid phone number was passed in: {phoneNumber}");
        }
    }

    internal class InvalidAuthenticationRequest : MicroserviceException
    {
        public InvalidAuthenticationRequest(string message) : base((int)HttpStatusCode.BadRequest, "InvalidAuthenticationRequest", message) { }
    }
}
