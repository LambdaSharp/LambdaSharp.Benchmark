#!/bin/bash
set -e

# ensure that an AWS profile has been selected
if [ -z "$AWS_PROFILE" ]; then
    echo "ERROR: environment variable \$AWS_PROFILE is not set"
    exit 1
fi

# obtain location of Source folder from script location
SOURCE=`dirname -- "$(readlink -f "${BASH_SOURCE}")"`
BIN="$SOURCE/../bin"
SUFFIX=$(cat /dev/urandom | tr -dc 'a-zA-Z0-9' | fold -w 4 | head -n 1)
AWS_ACCOUNT_ID=`aws sts get-caller-identity --query "Account" --output text`

# recreate build folder
if [ -d "$BIN" ]; then
    rm -rf "$BIN"
fi
mkdir "$BIN"

OUTPUT_FOLDER="$SOURCE/MinimalFunction/bin/Release/net6.0/linux-x64/publish/"

# clean-up previous build
rm -rf "$SOURCE/MinimalFunction/bin" "$SOURCE/MinimalFunction/obj" > /dev/null 2>&1

# build function
dotnet publish \
    --configuration Release \
    --framework net6.0 \
    --runtime linux-x64 \
    --no-self-contained \
    --output "$OUTPUT_FOLDER" \
    -property:GenerateRuntimeConfigurationFiles=true \
    -property:TieredCompilation=false \
    -property:TieredCompilationQuickJit=false \
    "$SOURCE/MinimalFunction/"

# compress function output into zip file
pushd "$OUTPUT_FOLDER"
zip -9 -r "$BIN/LambdaPerformance-MinimalFuntion-$SUFFIX.zip" .
popd

# create or update function
cd $BIN
set +e
aws lambda get-function --function-name LambdaPerformance-MinimalFunction > /dev/null 2>&1
if [ 0 -eq $? ]; then
    set -e
    aws lambda update-function-code \
        --function-name LambdaPerformance-MinimalFunction \
        --zip-file "fileb://LambdaPerformance-MinimalFuntion-$SUFFIX.zip"
else
    set -e
    aws lambda create-function \
        --function-name LambdaPerformance-MinimalFunction \
        --memory-size 256 \
        --zip-file "fileb://LambdaPerformance-MinimalFuntion-$SUFFIX.zip" \
        --handler "MinimalFunction::LambdaPerformance.MinimalFunction.Function::ProcessMessageStreamAsync" \
        --runtime dotnet6 \
        --role arn:aws:iam::$AWS_ACCOUNT_ID:role/LambdaDefaultRole
fi


