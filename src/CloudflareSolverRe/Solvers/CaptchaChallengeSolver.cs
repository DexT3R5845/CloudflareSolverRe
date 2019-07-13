﻿using CloudflareSolverRe.CaptchaProviders;
using CloudflareSolverRe.Types;
using CloudflareSolverRe.Types.Captcha;
using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CloudflareSolverRe.Solvers
{
    public class CaptchaChallengeSolver : ChallengeSolver
    {
        private readonly ICaptchaProvider captchaProvider;

        public CaptchaChallengeSolver(HttpClient client, HttpClientHandler handler, Uri siteUrl, DetectResult detectResult, ICaptchaProvider captchaProvider, [Optional]int maxRetries)
            : base(client, handler, siteUrl, detectResult, maxRetries)
        {
            this.captchaProvider = captchaProvider;
        }

        public CaptchaChallengeSolver(HttpClientHandler handler, Uri siteUrl, DetectResult detectResult, ICaptchaProvider captchaProvider, [Optional]int maxRetries)
            : base(handler, siteUrl, detectResult, maxRetries)
        {
            this.captchaProvider = captchaProvider;
        }


        private bool IsCaptchaSolvingEnabled() => captchaProvider != null;


        public new async Task<SolveResult> Solve()
        {
            if (!IsCaptchaSolvingEnabled())
            {
                return new SolveResult
                {
                    Success = false,
                    FailReason = "Missing captcha provider",
                    DetectResult = DetectResult,
                };
            }

            var solve = await SolveChallenge(DetectResult.Html);
            return new SolveResult
            {
                Success = solve.Success,
                FailReason = solve.FailReason,
                DetectResult = DetectResult,
            };
        }

        private async Task<SolveResult> SolveChallenge(string html)
        {
            var challenge = CaptchaChallenge.Parse(html, SiteUrl);

            var clearancePage = $"{SiteUrl.Scheme}://{SiteUrl.Host}{challenge.Action}";

            var result = await challenge.Solve(captchaProvider);

            if (!result.Success)
                return new SolveResult(false, LayerCaptcha, $"captcha provider error ({result.Response})", DetectResult);

            var solution = new CaptchaChallengeSolution(clearancePage, challenge.S, result.Response);

            return await SubmitCaptchaSolution(solution);
        }

        private async Task<SolveResult> SubmitCaptchaSolution(CaptchaChallengeSolution solution)
        {
            PrepareHttpHandler();

            var request = CreateRequest(new Uri(solution.ClearancePage));
            var response = await HttpClient.SendAsync(request);

            if (response.StatusCode.Equals(HttpStatusCode.Found))
            {
                var success = response.Headers.Contains("Set-Cookie");
                return new SolveResult(success, LayerCaptcha, success ? null : "response cookie not found", DetectResult, response);
            }
            else
            {
                return new SolveResult(false, LayerCaptcha, "something wrong happened", DetectResult, response); //"invalid submit response"
            }
        }

    }
}