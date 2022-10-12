#!/bin/bash

# obtain location of Source folder from script location
PROJECTS_FOLDER=`pwd`
BUILD_FOLDER="$PROJECTS_FOLDER/build"
PUBLISH_FOLDER="$PROJECTS_FOLDER/publish"

# check required environment variable is set
if [ "$PROJECT_SOURCE" == "" ]; then
    echo "ERROR: environment variable 'PROJECT_SOURCE' is not set"
    exit 1
fi

# recreate build folder
if [ -d "$BUILD_FOLDER" ]; then
    rm -rf "$BUILD_FOLDER"
fi
mkdir "$BUILD_FOLDER"

# recreate publish folder
if [ -d "$PUBLISH_FOLDER" ]; then
    rm -rf "$PUBLISH_FOLDER"
fi
mkdir "$PUBLISH_FOLDER"

###
# build_function PROJECTPATH FRAMEWORK ARCHITECTURE TIEREDCOMPILATION READYTORUN
#
#   PROJECTPATH: project folder or file path
#   FRAMEWORK: one of 'dotnet6' or 'dotnetcore3.1'
#   ARCHITECTURE: one of 'x86_64' or 'arm64'
#   TIEREDCOMPILATION: either 'yes' or 'no'
#   READYTORUN: either 'yes' or 'no'
###
function build_function() {

    # determine framework
    local FRAMEWORK_BUILD=""
    local FRAMEWORK_LABEL=""
    local LAMBDA_RUNTIME="$2"
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
            echo "Invalid framework: $2"
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
            echo "Invalid architecture: $3"
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
            echo "Invalid tiered compilation option: $4"
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
            echo "Invalid ready2run option: $5"
            exit 1
            ;;
    esac

    # create label based on configuration
    local FUNCTION_LABEL="$1-$FRAMEWORK_LABEL-$ARCHITECTURE_LABEL-$TIERED_LABEL-$READY2RUN_LABEL"

    # check expected files exist
    local FUNCTION_PROJECT="$PROJECTS_FOLDER/$1/$1.csproj"
    if [ ! -f "$FUNCTION_PROJECT" ]; then
        echo "Could not find project for $1"
        exit 1
    fi
    local RUNSPEC_FILE="$PROJECTS_FOLDER/$1/RunSpec.json"
    if [ ! -f "$RUNSPEC_FILE" ]; then
        echo "Could not find RunSpec.json for $1"
        exit 1
    fi

    # determine if function is self-contained and requires a custom runtime
    local SELF_CONTAINED_OPTION="--no-self-contained"
    if grep -F "<AssemblyName>bootstrap</AssemblyName>" $FUNCTION_PROJECT; then
        SELF_CONTAINED_OPTION="--self-contained"
        LAMBDA_RUNTIME="provided.al2"
    fi

    # build project
    echo ""
    echo "*** BUILDING $1 [$FRAMEWORK_LABEL, $ARCHITECTURE_LABEL, $TIERED_LABEL, $READY2RUN_LABEL]"
    local BUILD_FUNCTION_FOLDER="$BUILD_FOLDER/$FUNCTION_LABEL/"
    local LOG_FILE="$PUBLISH_FOLDER/$FUNCTION_LABEL.log"
    dotnet publish \
        --configuration Release \
        --framework $FRAMEWORK_BUILD \
        --runtime $ARCHITECTURE_BUILD \
        $SELF_CONTAINED_OPTION \
        --output "$BUILD_FUNCTION_FOLDER" \
        -property:GenerateRuntimeConfigurationFiles=true \
        -property:TieredCompilation=$TIERED_BUILD \
        -property:TieredCompilationQuickJit=$TIERED_BUILD \
        -property:PublishReadyToRun=$READY2RUN_BUILD \
        "$PROJECTS_FOLDER/$1/" > "$LOG_FILE"

    # check if the build was successful
    if [ -d $BUILD_FUNCTION_FOLDER ]; then
        if [ "$(ls -A $BUILD_FUNCTION_FOLDER)" ]; then

            # compress build output into zip file (NoPreJIT)
            local ZIP_FILE1="$PUBLISH_FOLDER/$FUNCTION_LABEL-NoPreJIT.zip"
            pushd "$BUILD_FUNCTION_FOLDER" > /dev/null
            zip -9 -r "$ZIP_FILE1" . > /dev/null
            popd > /dev/null
            local ZIP_SIZE="$(wc -c <"$ZIP_FILE1")"
            echo "==> Success: $ZIP_SIZE bytes"

            # copy JSON file and add runtime/architecture details
            cat "$PROJECTS_FOLDER/$1/RunSpec.json" \
                | jq ". += {\"Project\":\"$1\",\"Runtime\":\"$LAMBDA_RUNTIME\",\"Architecture\":\"$3\",\"ZipSize\":$ZIP_SIZE,\"Tiered\":\"$4\",\"Ready2Run\":\"$5\",\"PreJIT\":\"no\"}" \
                > "$PUBLISH_FOLDER/$FUNCTION_LABEL-NoPreJIT.json"

            # compress build output into zip file (YesPreJIT)
            local ZIP_FILE2="$PUBLISH_FOLDER/$FUNCTION_LABEL-YesPreJIT.zip"
            pushd "$BUILD_FUNCTION_FOLDER" > /dev/null
            zip -9 -r "$ZIP_FILE2" . > /dev/null
            popd > /dev/null
            local ZIP_SIZE="$(wc -c <"$ZIP_FILE2")"
            echo "==> Success: $ZIP_SIZE bytes"

            # copy JSON file and add runtime/architecture details
            cat "$PROJECTS_FOLDER/$1/RunSpec.json" \
                | jq ". += {\"Project\":\"$1\",\"Runtime\":\"$LAMBDA_RUNTIME\",\"Architecture\":\"$3\",\"ZipSize\":$ZIP_SIZE,\"Tiered\":\"$4\",\"Ready2Run\":\"$5\",\"PreJIT\":\"yes\"}" \
                > "$PUBLISH_FOLDER/$FUNCTION_LABEL-YesPreJIT.json"
        else

            # show build output and delete empty folder
            cat "$PUBLISH_FOLDER/$FUNCTION_LABEL.log"
            rm -rf "$BUILD_FUNCTION_FOLDER"
        fi
    fi
}

###
# Downoad and unzip the project file
###
PROJECT_NAME=`basename "$PROJECT_SOURCE" .zip`
PROJECT_ZIP="$PROJECT_NAME.zip"

aws s3 cp "$PROJECT_SOURCE" ./$PROJECT_ZIP
if [ ! -f "$PROJECT_ZIP" ]; then
    echo "ERROR: downlad failed for $PROJECT_SOURCE"
    exit 1
fi

unzip "./$PROJECT_ZIP"

###
# Build project configurations
###
build_function "$PROJECT_NAME" dotnet6          x86_64  no  no
build_function "$PROJECT_NAME" dotnet6          x86_64  no  yes
build_function "$PROJECT_NAME" dotnet6          x86_64  yes no
build_function "$PROJECT_NAME" dotnet6          x86_64  yes yes
build_function "$PROJECT_NAME" dotnet6          arm64   no  no
build_function "$PROJECT_NAME" dotnet6          arm64   no  yes
build_function "$PROJECT_NAME" dotnet6          arm64   yes no
build_function "$PROJECT_NAME" dotnet6          arm64   yes yes
build_function "$PROJECT_NAME" dotnetcore3.1    x86_64  no  no
build_function "$PROJECT_NAME" dotnetcore3.1    x86_64  no  yes
build_function "$PROJECT_NAME" dotnetcore3.1    x86_64  yes no
build_function "$PROJECT_NAME" dotnetcore3.1    x86_64  yes yes
build_function "$PROJECT_NAME" dotnetcore3.1    arm64   no  no
build_function "$PROJECT_NAME" dotnetcore3.1    arm64   no  yes
build_function "$PROJECT_NAME" dotnetcore3.1    arm64   yes no
build_function "$PROJECT_NAME" dotnetcore3.1    arm64   yes yes
