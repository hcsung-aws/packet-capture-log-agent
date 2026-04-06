terraform {
  required_version = ">= 1.5"
  required_providers {
    aws    = { source = "hashicorp/aws", version = "~> 5.0" }
    random = { source = "hashicorp/random", version = "~> 3.0" }
  }
}

provider "aws" {
  region = var.aws_region
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

# S3 Bucket
resource "aws_s3_bucket" "jobs" {
  bucket = "${var.project_name}-jobs-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket_server_side_encryption_configuration" "jobs" {
  bucket = aws_s3_bucket.jobs.id
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}

resource "aws_s3_bucket_public_access_block" "jobs" {
  bucket                  = aws_s3_bucket.jobs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "jobs" {
  bucket = aws_s3_bucket.jobs.id
  rule {
    id     = "expire-old-jobs"
    status = "Enabled"
    filter { prefix = "jobs/" }
    expiration { days = 30 }
  }
}

# Upload PROTOCOL_SCHEMA.md for Generation phase
resource "aws_s3_object" "schema" {
  bucket = aws_s3_bucket.jobs.id
  key    = "schema/PROTOCOL_SCHEMA.md"
  source = "${path.module}/../../docs/PROTOCOL_SCHEMA.md"
  etag   = filemd5("${path.module}/../../docs/PROTOCOL_SCHEMA.md")
}

# IAM Role for Lambda
resource "aws_iam_role" "lambda" {
  name = "${var.project_name}-lambda"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy" "lambda" {
  name = "${var.project_name}-lambda-policy"
  role = aws_iam_role.lambda.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = "arn:aws:logs:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:*"
      },
      {
        Effect   = "Allow"
        Action   = ["s3:GetObject", "s3:PutObject", "s3:ListBucket"]
        Resource = [aws_s3_bucket.jobs.arn, "${aws_s3_bucket.jobs.arn}/*"]
      },
      {
        Effect   = "Allow"
        Action   = ["bedrock:InvokeModel"]
        Resource = "*"
      },
    ]
  })
}

# IAM Role for Orchestrator Lambda (needs Step Functions access)
resource "aws_iam_role" "orchestrator" {
  name = "${var.project_name}-orchestrator"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy" "orchestrator" {
  name = "${var.project_name}-orchestrator-policy"
  role = aws_iam_role.orchestrator.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = "arn:aws:logs:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:*"
      },
      {
        Effect   = "Allow"
        Action   = ["s3:GetObject", "s3:PutObject", "s3:ListBucket", "s3:*"]
        Resource = [aws_s3_bucket.jobs.arn, "${aws_s3_bucket.jobs.arn}/*"]
      },
      {
        Effect   = "Allow"
        Action   = ["states:StartExecution", "states:DescribeExecution", "states:ListExecutions"]
        Resource = "*"
      },
    ]
  })
}

# IAM Role for Step Functions
resource "aws_iam_role" "sfn" {
  name = "${var.project_name}-sfn"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = { Service = "states.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy" "sfn" {
  name = "${var.project_name}-sfn-policy"
  role = aws_iam_role.sfn.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["lambda:InvokeFunction"]
      Resource = "arn:aws:lambda:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:function:${var.project_name}-*"
    }]
  })
}

# Lambda Layer (common modules)
data "archive_file" "layer" {
  type        = "zip"
  source_dir  = "${path.module}/../lambda/layer_pkg"
  output_path = "${path.module}/.build/layer.zip"
}

resource "aws_lambda_layer_version" "common" {
  filename            = data.archive_file.layer.output_path
  source_code_hash    = data.archive_file.layer.output_base64sha256
  layer_name          = "${var.project_name}-common"
  compatible_runtimes = ["python3.12"]
}

# Lambda Functions
locals {
  lambda_functions = {
    discovery  = { handler = "handler.handler", timeout = 300 }
    analysis   = { handler = "handler.handler", timeout = 600 }
    merge      = { handler = "handler.handler", timeout = 300 }
    generation = { handler = "handler.handler", timeout = 600 }
    validation = { handler = "handler.handler", timeout = 300 }
  }
  lambda_env = {
    S3_BUCKET        = aws_s3_bucket.jobs.id
    BEDROCK_MODEL_ID = var.bedrock_model_id
    BEDROCK_MAX_TOKENS = "16384"
    MAX_REVIEW_ROUNDS  = "3"
  }
}

data "archive_file" "lambda" {
  for_each    = local.lambda_functions
  type        = "zip"
  source_dir  = "${path.module}/../lambda/${each.key}"
  output_path = "${path.module}/.build/${each.key}.zip"
}

resource "aws_lambda_function" "fn" {
  for_each         = local.lambda_functions
  function_name    = "${var.project_name}-${each.key}"
  filename         = data.archive_file.lambda[each.key].output_path
  source_code_hash = data.archive_file.lambda[each.key].output_base64sha256
  handler          = each.value.handler
  runtime          = "python3.12"
  timeout          = each.value.timeout
  memory_size      = 256
  role             = aws_iam_role.lambda.arn
  layers           = [aws_lambda_layer_version.common.arn]

  environment {
    variables = local.lambda_env
  }
}

# Orchestrator Lambda
data "archive_file" "orchestrator" {
  type        = "zip"
  source_dir  = "${path.module}/../lambda/orchestrator"
  output_path = "${path.module}/.build/orchestrator.zip"
}

resource "aws_lambda_function" "orchestrator" {
  function_name    = "${var.project_name}-orchestrator"
  filename         = data.archive_file.orchestrator.output_path
  source_code_hash = data.archive_file.orchestrator.output_base64sha256
  handler          = "handler.handler"
  runtime          = "python3.12"
  timeout          = 120
  memory_size      = 512
  role             = aws_iam_role.orchestrator.arn

  environment {
    variables = {
      S3_BUCKET        = aws_s3_bucket.jobs.id
      SFN_PIPELINE_ARN = aws_sfn_state_machine.pipeline.arn
    }
  }
}

# Step Functions
resource "aws_sfn_state_machine" "pipeline" {
  name     = "${var.project_name}-pipeline"
  role_arn = aws_iam_role.sfn.arn

  definition = jsonencode({
    Comment = "Protocol Auto-Generation Pipeline"
    StartAt = "Discovery"
    States = {
      Discovery = {
        Type     = "Task"
        Resource = aws_lambda_function.fn["discovery"].arn
        ResultPath = "$.discovery_result"
        Next     = "PrepareAnalysis"
      }
      PrepareAnalysis = {
        Type = "Pass"
        Next = "AnalysisMap"
      }
      # Flatten: pass discovery result's relevant_files into map
      AnalysisMap = {
        Type      = "Map"
        ItemsPath = "$.discovery_result.relevant_files"
        ItemSelector = {
          "job_id.$" = "$.job_id"
          "file.$"   = "$$.Map.Item.Value"
        }
        MaxConcurrency = 3
        Iterator = {
          StartAt = "AnalyzeFile"
          States = {
            AnalyzeFile = {
              Type     = "Task"
              Resource = aws_lambda_function.fn["analysis"].arn
              End      = true
              Retry = [{
                ErrorEquals     = ["States.TaskFailed"]
                IntervalSeconds = 10
                MaxAttempts     = 2
                BackoffRate     = 2
              }]
            }
          }
        }
        ResultPath = "$.analysis_results"
        Next       = "Merge"
      }
      Merge = {
        Type     = "Task"
        Resource = aws_lambda_function.fn["merge"].arn
        Parameters = { "job_id.$" = "$.job_id" }
        ResultPath = "$.merge_result"
        Next     = "Generation"
      }
      Generation = {
        Type     = "Task"
        Resource = aws_lambda_function.fn["generation"].arn
        Parameters = { "job_id.$" = "$.job_id" }
        ResultPath = "$.generation_result"
        Next     = "Validation"
      }
      Validation = {
        Type     = "Task"
        Resource = aws_lambda_function.fn["validation"].arn
        Parameters = { "job_id.$" = "$.job_id" }
        ResultPath = "$.validation_result"
        Next     = "Done"
      }
      Done = {
        Type = "Succeed"
      }
    }
  })
}

# API Key
resource "random_password" "api_key" {
  length  = 32
  special = false
}

# API Key Authorizer Lambda
data "archive_file" "authorizer" {
  type        = "zip"
  source_dir  = "${path.module}/../lambda/authorizer"
  output_path = "${path.module}/.build/authorizer.zip"
}

resource "aws_iam_role" "authorizer" {
  name = "${var.project_name}-authorizer"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "authorizer_logs" {
  role       = aws_iam_role.authorizer.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_lambda_function" "authorizer" {
  function_name    = "${var.project_name}-authorizer"
  filename         = data.archive_file.authorizer.output_path
  source_code_hash = data.archive_file.authorizer.output_base64sha256
  handler          = "handler.handler"
  runtime          = "python3.12"
  timeout          = 5
  memory_size      = 128
  role             = aws_iam_role.authorizer.arn

  environment {
    variables = { API_KEY = random_password.api_key.result }
  }
}

# API Gateway
resource "aws_apigatewayv2_api" "api" {
  name          = "${var.project_name}-api"
  protocol_type = "HTTP"
  cors_configuration {
    allow_origins = ["*"]
    allow_methods = ["GET", "POST", "OPTIONS"]
    allow_headers = ["*"]
  }
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.api.id
  name        = "$default"
  auto_deploy = true
}

resource "aws_apigatewayv2_authorizer" "api_key" {
  api_id                            = aws_apigatewayv2_api.api.id
  authorizer_type                   = "REQUEST"
  authorizer_uri                    = aws_lambda_function.authorizer.invoke_arn
  authorizer_payload_format_version = "2.0"
  name                              = "api-key-authorizer"
  enable_simple_responses           = true
  identity_sources                  = ["$request.header.x-api-key"]
  authorizer_result_ttl_in_seconds  = 300
}

resource "aws_lambda_permission" "authorizer" {
  statement_id  = "AllowAPIGatewayAuthorizer"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.authorizer.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.api.execution_arn}/*"
}

resource "aws_apigatewayv2_integration" "orchestrator" {
  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.orchestrator.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "routes" {
  for_each = toset([
    "POST /jobs",
    "POST /jobs/{id}/pipeline",
    "GET /jobs/{id}/status",
    "GET /jobs/{id}/result",
  ])
  api_id             = aws_apigatewayv2_api.api.id
  route_key          = each.value
  target             = "integrations/${aws_apigatewayv2_integration.orchestrator.id}"
  authorization_type = "CUSTOM"
  authorizer_id      = aws_apigatewayv2_authorizer.api_key.id
}

resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.orchestrator.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.api.execution_arn}/*/*"
}
