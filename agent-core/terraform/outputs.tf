output "api_endpoint" {
  value = aws_apigatewayv2_api.api.api_endpoint
}

output "s3_bucket" {
  value = aws_s3_bucket.jobs.id
}

output "sfn_arn" {
  value = aws_sfn_state_machine.pipeline.arn
}

output "api_key" {
  value     = random_password.api_key.result
  sensitive = true
}
