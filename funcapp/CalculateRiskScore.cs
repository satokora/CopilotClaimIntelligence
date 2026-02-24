using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace sk.poc
{
    public class CalculateRiskScore
    {
        private readonly ILogger<CalculateRiskScore> _logger;
        

        public CalculateRiskScore(ILogger<CalculateRiskScore> logger)
        {
            _logger = logger;
        }

        [Function("CalculateRiskScore")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Risk Scoring initiated.");
            var requestBody=new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("RequestBody: " + requestBody.Result);

            try
            {
                var claim=JsonSerializer.Deserialize<ClaimInput>(requestBody.Result, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
                double score=0;
                bool escalation=false;
                var riskFactors=new List<string>();
                

                if(claim!=null)
                {
                    _logger.LogInformation("Claim details: Amount={Amount}, Region={Region}, Type={Type}",claim.ClaimAmount, claim.Region, claim.ClaimType);
                    if (claim.ClaimAmount > 50000)
                    {
                        score += 0.4;
                        riskFactors.Add("High claim amount");
                        _logger.LogInformation("New Risk Factor: High claim amount");
                    }
                    else if (claim.ClaimAmount > 25000)
                    {
                        score+=0.2;
                        riskFactors.Add("Moderate claim amount");
                        _logger.LogInformation("New Risk Factor: Moderate claim amount");
                    }

                    if (claim.Region=="NY")
                    {
                        score += 0.2;
                        riskFactors.Add("High risk region");
                        _logger.LogInformation("New Risk Factor: High Risk region");
                    }

                    if(claim.ClaimType=="Liability")
                    {
                        score += 0.2;
                        riskFactors.Add("Liability claim type");
                        _logger.LogInformation("New Risk Factor: Liability claim type");
                    }

                    if (!string.IsNullOrEmpty(claim.Description) &&
                    claim.Description.ToLower().Contains("multiple"))
                    {
                        score += 0.1;
                        riskFactors.Add("Multiple vehicles involved");
                        _logger.LogInformation("New Risk Factor: Multiple vehicles involved");
                    }

                    score = Math.Min(score, 1.0);
                    escalation = score >= 0.7;

                    _logger.LogInformation("RiskEvaluationCompleted | ClaimAmount:{ClaimAmount} | RiskScore:{RiskScore} | Escalation:{Escalation}",claim.ClaimAmount, score, escalation);

                    var responseObj = new
                    {
                        riskScore = score,
                        escalation = escalation,
                        riskFactors = riskFactors
                    };

                    return new OkObjectResult(responseObj);
                }
                else
                {
                    _logger.LogError("requestBody is null or doesn't match with expected format.");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Risk scoring failed.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            

        }

        private class ClaimInput
        {
            public double ClaimAmount { get; set; }
            public string? Region { get; set; }
            public string? ClaimType { get; set; }
            public string? Description { get; set; }
        }
    }

}
