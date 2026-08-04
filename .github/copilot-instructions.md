# Copilot Instructions

## Project Guidelines
- User prefers enterprise-style AI architecture: AIService must depend on abstractions (IAIProvider, IAIPromptBuilder), prompt construction must be isolated in a prompt builder, AI providers must be swappable via DI, and AI settings (Provider/Model/ApiKey/Temperature/MaxTokens) must be in configuration options.