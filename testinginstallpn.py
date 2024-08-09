import os
import re

# Define the path to the file
file_path = os.path.join("src", "app", "plugins", "plugins.routing.ts")

# Read the content of the file
with open(file_path, "r") as file:
    content = file.read()

# Define the replacements
replacements = [
    (r"// INSERT ROUTES HERE", "  {\n// INSERT ROUTES HERE"),
    (r"// INSERT ROUTES HERE", "    path: 'time-planning-pn',\n// INSERT ROUTES HERE"),
    (r"// INSERT ROUTES HERE", "    loadChildren: () => import('./modules/time-planning-pn/time-planning-pn.module')\n// INSERT ROUTES HERE"),
    (r"// INSERT ROUTES HERE", "      .then(m => m.TimePlanningPnModule)\n// INSERT ROUTES HERE"),]
    (r"// INSERT ROUTES HERE", "  },\n// INSERT ROUTES HERE"),

# Apply each replacement in sequence
for pattern, replacement in replacements:
    content = re.sub(pattern, replacement, content, count=1)

# Write the modified content back to the file
with open(file_path, "w") as file:
    file.write(content)
