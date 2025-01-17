// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gameboard.Api
{
        public static class TicketAuthentication
    {
        public const string AuthenticationScheme = "Ticket";
        public const string AltSchemeName = "Bearer";
        public const string AuthorizationHeaderName = "Authorization";
        public const string ChallengeHeaderName = "WWW-Authenticate";
        public const string QuerystringField = "access_token";
        public const string TicketCachePrefix = "tkt:";

        public static class ClaimNames
        {
            public const string Subject = "sub";
            public const string Name = "name";
        }

    }
    public class TicketAuthenticationHandler : AuthenticationHandler<TicketAuthenticationOptions>
    {
        private readonly IDistributedCache _cache;

        public TicketAuthenticationHandler(
            IOptionsMonitor<TicketAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IDistributedCache cache
        )
            : base(options, logger, encoder, clock)
        {
            _cache = cache;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Task.Delay(0);

            string key = Request.Query[TicketAuthentication.AuthenticationScheme];

            if (string.IsNullOrEmpty(key))
                key = Request.Query[TicketAuthentication.QuerystringField];

            if (string.IsNullOrEmpty(key))
            {
                string[] authHeader = Request.Headers[TicketAuthentication.AuthorizationHeaderName].ToString().Split(' ');
                string scheme = authHeader[0];
                if (authHeader.Length > 1
                    && (scheme.Equals(TicketAuthentication.AuthenticationScheme, StringComparison.OrdinalIgnoreCase)
                    || scheme.Equals(TicketAuthentication.AltSchemeName, StringComparison.OrdinalIgnoreCase))
                ) {
                    key = authHeader[1];
                }
            }

            if (string.IsNullOrEmpty(key))
                return AuthenticateResult.NoResult();

            if (!key.StartsWith(TicketAuthentication.TicketCachePrefix))
                key = TicketAuthentication.TicketCachePrefix + key;

            string value = await _cache.GetStringAsync(key);

            if (!value.HasValue())
                return AuthenticateResult.NoResult();

            await _cache.RemoveAsync(key);

            string identity = value.Untagged();

            string name = value.Tag();

            string subject = Guid.TryParse(identity, out Guid guid)
                ? guid.ToString()
                : "";

            if (subject == null) // || expired)
                return AuthenticateResult.NoResult();

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new Claim[] {
                        new Claim(TicketAuthentication.ClaimNames.Subject, subject),
                        new Claim(TicketAuthentication.ClaimNames.Name, name)
                    },
                    Scheme.Name
                )
            );

            return AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)
            );
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers[TicketAuthentication.ChallengeHeaderName] = TicketAuthentication.AuthenticationScheme;

            await base.HandleChallengeAsync(properties);
        }
    }

    public class TicketAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

}
