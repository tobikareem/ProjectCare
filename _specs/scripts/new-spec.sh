#!/bin/bash

# Spec-Driven Development: New Spec Creator
# Creates requirements, design, and tasks specs from templates

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if feature name is provided
if [ -z "$1" ]; then
    echo -e "${YELLOW}Usage: $0 \"Feature Name\"${NC}"
    echo "Example: $0 \"GPS Check-In\""
    exit 1
fi

FEATURE_NAME="$1"
# Convert to lowercase and replace spaces with hyphens
FEATURE_SLUG=$(echo "$FEATURE_NAME" | tr '[:upper:]' '[:lower:]' | tr ' ' '-')

# Get script directory (where this script is located)
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SPECS_DIR="$(dirname "$SCRIPT_DIR")"

# Paths
TEMPLATES_DIR="$SPECS_DIR/templates"
REQUIREMENTS_DIR="$SPECS_DIR/01-requirements"
DESIGN_DIR="$SPECS_DIR/02-design"
TASKS_DIR="$SPECS_DIR/03-tasks"

# Check if templates exist
if [ ! -f "$TEMPLATES_DIR/REQUIREMENTS_TEMPLATE.md" ]; then
    echo -e "${YELLOW}Error: Templates not found in $TEMPLATES_DIR${NC}"
    exit 1
fi

echo -e "${BLUE}Creating specs for: $FEATURE_NAME${NC}"
echo -e "Feature slug: $FEATURE_SLUG"
echo ""

# Create directories if they don't exist
mkdir -p "$REQUIREMENTS_DIR"
mkdir -p "$DESIGN_DIR"
mkdir -p "$TASKS_DIR"

# File paths
REQ_FILE="$REQUIREMENTS_DIR/${FEATURE_SLUG}.md"
DESIGN_FILE="$DESIGN_DIR/${FEATURE_SLUG}.md"
TASKS_FILE="$TASKS_DIR/${FEATURE_SLUG}.md"

# Check if files already exist
if [ -f "$REQ_FILE" ]; then
    echo -e "${YELLOW}Warning: $REQ_FILE already exists. Skipping requirements.${NC}"
else
    # Copy requirements template
    cp "$TEMPLATES_DIR/REQUIREMENTS_TEMPLATE.md" "$REQ_FILE"
    # Replace placeholders
    sed -i "s/\[Feature Name\]/$FEATURE_NAME/g" "$REQ_FILE"
    sed -i "s/YYYY-MM-DD/$(date +%Y-%m-%d)/g" "$REQ_FILE"
    sed -i "s/\[Your Name\]/$USER/g" "$REQ_FILE"
    # Update cross-links
    sed -i "s|\[Link to 02-design spec\]|[Design Spec](../02-design/${FEATURE_SLUG}.md)|g" "$REQ_FILE"
    sed -i "s|\[Link to 03-tasks spec\]|[Tasks Spec](../03-tasks/${FEATURE_SLUG}.md)|g" "$REQ_FILE"
    echo -e "${GREEN}✓ Created: $REQ_FILE${NC}"
fi

if [ -f "$DESIGN_FILE" ]; then
    echo -e "${YELLOW}Warning: $DESIGN_FILE already exists. Skipping design.${NC}"
else
    # Copy design template
    cp "$TEMPLATES_DIR/DESIGN_TEMPLATE.md" "$DESIGN_FILE"
    # Replace placeholders
    sed -i "s/\[Feature Name\]/$FEATURE_NAME/g" "$DESIGN_FILE"
    sed -i "s/YYYY-MM-DD/$(date +%Y-%m-%d)/g" "$DESIGN_FILE"
    sed -i "s/\[Your Name\]/$USER/g" "$DESIGN_FILE"
    # Update cross-links
    sed -i "s|\[Link to 01-requirements spec\]|[Requirements Spec](../01-requirements/${FEATURE_SLUG}.md)|g" "$DESIGN_FILE"
    sed -i "s|\[Link to 03-tasks spec\]|[Tasks Spec](../03-tasks/${FEATURE_SLUG}.md)|g" "$DESIGN_FILE"
    sed -i "s|\[feature-name\]|${FEATURE_SLUG}|g" "$DESIGN_FILE"
    echo -e "${GREEN}✓ Created: $DESIGN_FILE${NC}"
fi

if [ -f "$TASKS_FILE" ]; then
    echo -e "${YELLOW}Warning: $TASKS_FILE already exists. Skipping tasks.${NC}"
else
    # Copy tasks template
    cp "$TEMPLATES_DIR/TASKS_TEMPLATE.md" "$TASKS_FILE"
    # Replace placeholders
    sed -i "s/\[Feature Name\]/$FEATURE_NAME/g" "$TASKS_FILE"
    sed -i "s/YYYY-MM-DD/$(date +%Y-%m-%d)/g" "$TASKS_FILE"
    sed -i "s/\[Your Name\]/$USER/g" "$TASKS_FILE"
    # Update cross-links
    sed -i "s|\[Link to 01-requirements spec\]|[Requirements Spec](../01-requirements/${FEATURE_SLUG}.md)|g" "$TASKS_FILE"
    sed -i "s|\[Link to 02-design spec\]|[Design Spec](../02-design/${FEATURE_SLUG}.md)|g" "$TASKS_FILE"
    sed -i "s|\[feature-name\]|${FEATURE_SLUG}|g" "$TASKS_FILE"
    echo -e "${GREEN}✓ Created: $TASKS_FILE${NC}"
fi

echo ""
echo -e "${GREEN}✅ Spec files created successfully!${NC}"
echo ""
echo "Next steps:"
echo "1. Edit the requirements spec:"
echo -e "   ${BLUE}$REQ_FILE${NC}"
echo "2. Get stakeholder approval"
echo "3. Edit the design spec:"
echo -e "   ${BLUE}$DESIGN_FILE${NC}"
echo "4. Get tech lead approval"
echo "5. Edit the tasks spec:"
echo -e "   ${BLUE}$TASKS_FILE${NC}"
echo "6. Get team approval"
echo "7. Implement with Claude Code:"
echo -e "   ${BLUE}claude-code --spec _specs/03-tasks/${FEATURE_SLUG}.md${NC}"
echo ""
