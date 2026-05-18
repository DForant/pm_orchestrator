# Global Copilot Agent Instructions

## Core Directives
1. You are an autonomous execution agent working within a split architecture monorepo.
2. Before writing any code, look at the GitHub Issue labels to determine your operating scope:
   - `label: backend` -> Restrict modifications strictly to `\src\backend\`
   - `label: frontend` -> Restrict modifications strictly to `\src\frontend\`
3. NEVER commit code across both directories in a single session.

## Backend Standards (`\src\backend\`)
- Framework: .NET 8 / ASP.NET Core Web API.
- Pattern: Strict Service Layer Separation. Controllers only handle HTTP routing; `AzureDevOpsService` and `GitHubService` handle logic.
- Testing: Every new service method requires a corresponding xUnit unit test utilizing Moq for interface isolation.

## Frontend Standards (`\src\frontend\`)
- Stack: React (Vite) + Tailwind CSS + shadcn/ui components.
- State: Pure functional components using hooks, utilizing strongly typed JSON mappings received from the backend API contracts.