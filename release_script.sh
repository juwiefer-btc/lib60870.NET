#!/bin/bash

###### Function to replace tabs with spaces in all .cs files################
format_cs_files() {
  echo "Formatting .cs files: Replacing tabs with spaces..."
  find . -type f -name "*.cs" -exec sed -i 's/\t/    /g' {} +
  echo "Formatting completed!"
}

#########Clean directory#########################
clean_build_directories()
{
  echo "Removing ./vs, bin/, obj/ folders, any .git directories or files, and doxydoc.NET folder..."
  find "$FOLDER_NAME" -type d \( -name "vs" -o -name "bin" -o -name "obj" -o -name "doxydoc.NET" -o -name ".git" \) -exec rm -rf {} +
  find "$FOLDER_NAME" -type f -name ".git" -exec rm -f {} +
  echo "Cleanup completed!"
}

#########Prepare release#############################
# Function to prepare folder
prepare_folder() {
	#Create doxigen
	rm -rf doxydoc.NET
	#doxygen  doxygen/Doxyfile

	#Create user guide
	asciidoctor  user_guide_dotnet.adoc
}

##########Create release folder#####################
create_release_folder()
{
	# Print the value
	echo "Creating folder: $FOLDER_NAME"

	# Create the folder
	mkdir -p "$FOLDER_NAME"

	echo "Folder '$FOLDER_NAME' created successfully!"

	cp -rf .net8 $FOLDER_NAME
	cp -rf examples $FOLDER_NAME
	cp -rf lib60870 $FOLDER_NAME
	cp -rf tests $FOLDER_NAME
	cp -rf CHANGELOG $FOLDER_NAME
	cp -rf COPYING $FOLDER_NAME
	cp -rf lib60870.NET.sln $FOLDER_NAME
	cp -rf README.md $FOLDER_NAME
	cp -rf user_guide_dotnet.adoc $FOLDER_NAME
	cp -rf doxygen $FOLDER_NAME
}

################ Function to create a tar.gz archive############################
compress_to_tar() {
  ARCHIVE_NAME="$FOLDER_NAME.tar.gz"
  echo "Creating archive: $ARCHIVE_NAME"
  tar -czf "$ARCHIVE_NAME" -C "$(dirname "$FOLDER_NAME")" "$(basename "$FOLDER_NAME")"
  echo "Archive '$ARCHIVE_NAME' created successfully!"
}

# Wait for user input if arguments are missing
while [ -z "$1" ]; do
  read -p "Enter version: " VERSION_NAME_INPUT
  set -- "$VERSION_NAME_INPUT" "$2"
done

while [ -z "$2" ]; do
  read -p "Enter option ([0]prepare/[1]release/[2]formatFiles/[3]all): " OPTION_INPUT
  set -- "$1" "$OPTION_INPUT"
done

# Store arguments
PREFIX="../lib60870.NET-"
FOLDER_NAME="${PREFIX}${1}"
OPTION="$2"

# Execute option case
case "$OPTION" in
  0)
    prepare_folder
    ;;
  1)
    create_release_folder
    ;;
2)
	format_cs_files
	;;
  3)
	format_cs_files
    prepare_folder
	create_release_folder
	clean_build_directories
	compress_to_tar
    ;;
  *)
    echo "Invalid option. Use 'prepare', 'release', or 'delete'."
    exit 1
    ;;
esac


#####################################################
