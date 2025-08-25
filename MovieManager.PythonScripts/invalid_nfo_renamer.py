import os
from datetime import datetime

# Configuration
root_dir = r"J:\MyFile\New\有码\演员"
target_date_str = '2025-04-17'
target_date = datetime.strptime(target_date_str, '%Y-%m-%d').date()

# Traverse directory
for dirpath, dirnames, filenames in os.walk(root_dir):
    # Count .nfo files in the current subfolder
    nfo_files = [f for f in filenames if f.lower().endswith('.nfo')]
    
    if len(nfo_files) < 2:
        continue  # Skip folders with fewer than 2 .nfo files

    for filename in nfo_files:
        file_path = os.path.join(dirpath, filename)

        # Get modification date
        mod_time = datetime.fromtimestamp(os.path.getmtime(file_path)).date()
        if mod_time == target_date:
            # Read content
            try:
                with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                    content = f.read()
            except Exception as e:
                print(f"Error reading {file_path}: {e}")
                continue

            # Check for missing tags
            if '<studio>' not in content and '<genre>' not in content and 'label' not in content:
                new_path = file_path.rsplit('.', 1)[0] + '.invalidnfo'
                try:
                    os.rename(file_path, new_path)
                    print(f"Renamed: {file_path} -> {new_path}")
                except Exception as e:
                    print(f"Error renaming {file_path}: {e}")
