# Copilot Instructions

## Project Guidelines
- User prefers enterprise-style AI architecture: AIService must depend on abstractions (IAIProvider, IAIPromptBuilder), prompt construction must be isolated in a prompt builder, AI providers must be swappable via DI, and AI settings (Provider/Model/ApiKey/Temperature/MaxTokens) must be in configuration options.
- User prefers free-cost AI setup for development/testing and wants multi-provider routing to avoid rate limits. Only APIs, not local like Ollama.
- Project environment setup is local-only (no production environment currently); prefer local-focused configuration changes.

## UI Preferences
- User prefers concise SaaS-style Settings UI: remove Notifications and Appearance sections, keep a single bottom Save button styled to look modern, style readonly account fields distinctly, separate Security and Danger Zone actions, and include polished hover/spacing/icon refinements.
- In Settings > Storage, show only two summary fields (Total Storage Used and Total Stored Files), aggregate everything under All (Documents + Health Reports), and show a small italic remaining-space text in the storage-used card.
- Security tab should show only two action buttons initially (Change Password, Delete Account), each opening its own popup modal; label should be 'Security'; hide Save Changes in Storage/Security/About.

## Interaction Guidelines
- When requested, provide explanation-only responses without code changes or code snippets.