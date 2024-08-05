import os
import shutil

# Change directory to the user's home directory
os.chdir(os.path.expanduser("~"))

# Print the current working directory
print("Current working directory:", os.getcwd())

# Define base paths
src_base = os.path.join("Documents", "workspace", "microting", "eform-angular-timeplanning-plugin")
dst_base = os.path.join("Documents", "workspace", "microting", "eform-angular-frontend")

# Paths to remove and copy
paths = [
    (os.path.join("eform-client", "src", "app", "plugins", "modules", "time-planning-pn"),
     os.path.join("eform-client", "src", "app", "plugins", "modules", "time-planning-pn")),
    (os.path.join("eFormAPI", "Plugins", "TimePlanning.Pn"),
     os.path.join("eFormAPI", "Plugins", "TimePlanning.Pn")),
]

# Remove and copy directories
for dst_rel_path, src_rel_path in paths:
    dst_path = os.path.join(dst_base, dst_rel_path)
    src_path = os.path.join(src_base, src_rel_path)

    # Remove the destination directory if it exists
    if os.path.exists(dst_path):
        shutil.rmtree(dst_path)

    # Copy the source directory to the destination
    shutil.copytree(src_path, dst_path)

# Ensure the Plugins directory exists
plugins_dir = os.path.join(dst_base, "eFormAPI", "Plugins")
os.makedirs(plugins_dir, exist_ok=True)

# List of test files and directories to remove
test_files_to_remove = [
    os.path.join("eform-client", "e2e", "Tests", "time-planning-settings"),
    os.path.join("eform-client", "e2e", "Tests", "time-planning-general"),
    os.path.join("eform-client", "e2e", "Page objects", "TimePlanning"),
    os.path.join("eform-client", "wdio-headless-plugin-step2a.conf.ts"),
    os.path.join("eform-client", "cypress", "e2e", "plugins", "time-planning-pn"),
]

# Remove the test files and directories
for rel_path in test_files_to_remove:
    full_path = os.path.join(dst_base, rel_path)
    if os.path.exists(full_path):
        if os.path.isdir(full_path):
            shutil.rmtree(full_path)
        else:
            os.remove(full_path)

# Ensure the plugins directory exists within the Cypress structure
cypress_plugins_dir = os.path.join(dst_base, "eform-client", "cypress", "e2e", "plugins")
os.makedirs(cypress_plugins_dir, exist_ok=True)

# List of test files and directories to copy
test_files_to_copy = [
    (os.path.join("eform-client", "e2e", "Tests", "time-planning-settings"),
     os.path.join("eform-client", "e2e", "Tests", "time-planning-settings")),
    (os.path.join("eform-client", "e2e", "Tests", "time-planning-general"),
     os.path.join("eform-client", "e2e", "Tests", "time-planning-general")),
    (os.path.join("eform-client", "e2e", "Page objects", "TimePlanning"),
     os.path.join("eform-client", "e2e", "Page objects", "TimePlanning")),
    (os.path.join("eform-client", "wdio-headless-plugin-step2.conf.ts"),
     os.path.join("eform-client", "wdio-headless-plugin-step2a.conf.ts")),
    (os.path.join("eform-client", "cypress", "e2e", "plugins", "time-planning-pn"),
     os.path.join("eform-client", "cypress", "e2e", "plugins", "time-planning-pn")),
]

# Copy the test files and directories
for src_rel_path, dst_rel_path in test_files_to_copy:
    src_path = os.path.join(src_base, src_rel_path)
    dst_path = os.path.join(dst_base, dst_rel_path)

    # Copy the source directory or file to the destination
    if os.path.isdir(src_path):
        shutil.copytree(src_path, dst_path)
    else:
        shutil.copy2(src_path, dst_path)
