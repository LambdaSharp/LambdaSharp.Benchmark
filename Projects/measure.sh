#!/bin/bash

SCRIPT_NAME=`basename "${0}"`

# validate arguments
if [ -z "$1" ]; then
    echo "ERROR: $SCRIPT_NAME PROJECT_FOLDER S3_BUCKET_NAME"
    exit 1
fi
if [ -z "$2" ]; then
    echo "ERROR: $SCRIPT_NAME PROJECT_FOLDER S3_BUCKET_NAME"
    exit 1
fi
if [ ! -d "$1" ]; then
    echo "ERROR: folder '$1' does not exist"
    exit 1
fi

FOLDER_NAME=`basename "${1}"`
ZIP_FILE="$FOLDER_NAME.zip"

# remove any existing build artifacts, we don't need them
find "$1" -name bin -or -name obj | xargs rm -rf

# zip folder and uplaod it
if [ -f "$ZIP_FILE" ]; then
    rm "$ZIP_FILE"
fi
zip -9 -r "$ZIP_FILE" "$1" > /dev/null
aws s3 cp "$ZIP_FILE" "s3://$2/Projets/$ZIP_FILE"
rm "$ZIP_FILE"
