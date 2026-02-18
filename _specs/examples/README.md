# Spec Examples

This folder contains example specifications demonstrating the Spec-Driven Development workflow for CarePath Health.

## Purpose

These examples show:
- How to fill in the templates
- What level of detail is expected
- How requirements → design → tasks flow together
- Best practices for .NET 9 full stack development

## Available Examples

### Coming Soon: GPS Check-In Feature
A complete example showing:
- Requirements spec for GPS-based caregiver check-in/out
- Design spec covering all layers (Domain, Application, Infrastructure, API, MAUI)
- Tasks spec with 47 atomic tasks broken down by phase

## How to Use Examples

1. **Study the examples** to understand the expected level of detail
2. **Copy patterns** that apply to your feature
3. **Adapt to your needs** - your feature may be simpler or more complex
4. **Reference the templates** in `../templates/` for blank starting points

## Tips for Writing Good Specs

### Requirements
- Focus on the **problem** and **user needs**, not the solution
- Use **Gherkin format** for user stories (Given/When/Then)
- Include **quantitative success criteria** (95% success rate, < 500ms)
- Consider both service lines: In-Home Care and Healthcare Staffing

### Design
- Start with **which layers** are affected (Domain, Application, Infrastructure, API, UI)
- Define **domain entities first** (follow DDD principles)
- Plan **EF Core migrations** carefully
- Consider **SignalR** for real-time features
- Think about **offline mode** for mobile app

### Tasks
- Keep tasks **atomic** (1-4 hours each)
- Define **clear dependencies** between tasks
- Include **specific file paths** to create/modify
- Write **testable success criteria** (not vague "works" statements)
- Organize by **phase** (Domain → Application → Infrastructure → API → UI → Testing → Deployment)

---

**Note**: The examples here are based on the CarePath Health architecture defined in `/Documentation/Architecture.md`. Always reference that document when creating specs for this project.
