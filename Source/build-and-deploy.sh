#!/bin/bash

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

function build_function() {

    # clean-up previous build
    rm -rf "$SOURCE/$1/bin" "$SOURCE/$1/obj" > /dev/null 2>&1

    # determine framework
    local FRAMEWORK_BUILD=""
    local FRAMEWORK_LABEL=""
    case $2 in
        dotnet6)
            FRAMEWORK_BUILD="net6.0"
            FRAMEWORK_LABEL="Net6"
            ;;
        dotnetcore3.1)
            FRAMEWORK_BUILD="netcoreapp3.1"
            FRAMEWORK_LABEL="Core31"
            ;;
        *)
            echo "Invalid framework: $1"
            exit 1
            ;;
    esac

    # determine architecture
    local ARCHITECTURE_BUILD="";
    local ARCHITECTURE_LABEL="";
    case $3 in
        x86_64)
            ARCHITECTURE_BUILD="linux-x64"
            ARCHITECTURE_LABEL="x64"
            ;;
        arm64)
            ARCHITECTURE_BUILD="linux-arm64"
            ARCHITECTURE_LABEL="Arm64"
            ;;
        *)
            echo "Invalid architecture: $2"
            exit 1
            ;;
    esac

    # determine tiered compilation option
    local TIERED_BUILD="";
    local TIERED_LABEL="";
    case $4 in
        yes)
            TIERED_BUILD="true";
            TIERED_LABEL="YesTC";
            ;;
        no)
            TIERED_BUILD="false";
            TIERED_LABEL="NoTC";
            ;;
        *)
            echo "Invalid tiered compilation option: $3"
            exit 1
            ;;
    esac

    # determine ready2run option
    local READY2RUN_BUILD="";
    local READY2RUN_LABEL="";
    case $5 in
        yes)
            READY2RUN_BUILD="true";
            READY2RUN_LABEL="YesR2R";
            ;;
        no)
            READY2RUN_BUILD="false";
            READY2RUN_LABEL="NoR2R";
            ;;
        *)
            echo "Invalid ready2run option: $4"
            exit 1
            ;;
    esac
    local FUNCTION_LABEL="$1-$FRAMEWORK_LABEL-$ARCHITECTURE_LABEL-$TIERED_LABEL-$READY2RUN_LABEL"

    # build project
    echo ""
    echo "*** BUILDING $1 [$FRAMEWORK_LABEL, $ARCHITECTURE_LABEL, $TIERED_LABEL, $READY2RUN_LABEL]"
    OUTPUT_FOLDER="$BIN/$FUNCTION_LABEL/"
    dotnet publish \
        --configuration Release \
        --framework $FRAMEWORK_BUILD \
        --runtime $ARCHITECTURE_BUILD \
        --no-self-contained \
        --output "$OUTPUT_FOLDER" \
        -property:GenerateRuntimeConfigurationFiles=true \
        -property:TieredCompilation=$TIERED_BUILD \
        -property:TieredCompilationQuickJit=$TIERED_BUILD \
        -property:PublishReadyToRun=$READY2RUN_BUILD \
        "$SOURCE/$1/" > "$BIN/$FUNCTION_LABEL.log"

    # check if the build was successful
    if [ -d $OUTPUT_FOLDER ]; then
        if [ "$(ls -A $OUTPUT_FOLDER)" ]; then
            local ZIP_FILE="$BIN/$FUNCTION_LABEL-$SUFFIX.zip"

            # compress build output into zip file
            pushd "$OUTPUT_FOLDER" > /dev/null
            zip -9 -r "$ZIP_FILE" . > /dev/null
            popd > /dev/null
            echo "==> Success: $(wc -c <"$ZIP_FILE") bytes"
        else

            # show build output and delete empty folder
            cat "$BIN/$FUNCTION_LABEL.log"
            rm -rf "$OUTPUT_FOLDER"
        fi
    fi
}

build_function MinimalFunction dotnet6 x86_64 no no
build_function MinimalFunction dotnet6 x86_64 no yes
build_function MinimalFunction dotnet6 x86_64 yes no
build_function MinimalFunction dotnet6 x86_64 yes yes

build_function MinimalFunction dotnet6 arm64 no no
build_function MinimalFunction dotnet6 arm64 no yes
build_function MinimalFunction dotnet6 arm64 yes no
build_function MinimalFunction dotnet6 arm64 yes yes

build_function MinimalFunction dotnetcore3.1 x86_64 no no
# build_function MinimalFunction dotnetcore3.1 x86_64 no yes
build_function MinimalFunction dotnetcore3.1 x86_64 yes no
# build_function MinimalFunction dotnetcore3.1 x86_64 yes yes

build_function MinimalFunction dotnetcore3.1 arm64 no no
# build_function MinimalFunction dotnetcore3.1 arm64 no yes
build_function MinimalFunction dotnetcore3.1 arm64 yes no
# build_function MinimalFunction dotnetcore3.1 arm64 yes yes

# create or update function
# cd $BIN
# +e
# aws lambda get-function --function-name LambdaPerformance-MinimalFunction > /dev/null 2>&1
# if [ 0 -eq $? ]; then
#     aws lambda update-function-code \
#         --function-name LambdaPerformance-MinimalFunction \
#         --zip-file "fileb://LambdaPerformance-MinimalFuntion-$SUFFIX.zip"
# else
#     aws lambda create-function \
#         --function-name LambdaPerformance-MinimalFunction \
#         --memory-size 256 \
#         --zip-file "fileb://LambdaPerformance-MinimalFuntion-$SUFFIX.zip" \
#         --handler "MinimalFunction::LambdaPerformance.MinimalFunction.Function::ProcessMessageStreamAsync" \
#         --runtime dotnet6 \
#         --role arn:aws:iam::$AWS_ACCOUNT_ID:role/LambdaDefaultRole
# fi
