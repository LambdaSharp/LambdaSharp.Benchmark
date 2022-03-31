#!/bin/bash

# obtain location of Source folder from script location
SCRIPT_FOLDER=`dirname -- "$(readlink -f "${BASH_SOURCE}")"`
PROJECTS_FOLDER="$SCRIPT_FOLDER"
BUILD_FOLDER="$SCRIPT_FOLDER/build"
PUBLISH_FOLDER="$SCRIPT_FOLDER/publish"

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

    # clean-up previous build
    rm -rf "$PROJECTS_FOLDER/$1/bin" "$PROJECTS_FOLDER/$1/obj" > /dev/null 2>&1

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

    # build project
    echo ""
    echo "*** BUILDING $1 [$FRAMEWORK_LABEL, $ARCHITECTURE_LABEL, $TIERED_LABEL, $READY2RUN_LABEL]"
    local BUILD_FUNCTION_FOLDER="$BUILD_FOLDER/$FUNCTION_LABEL/"
    local LOG_FILE="$PUBLISH_FOLDER/$FUNCTION_LABEL.log"
    dotnet publish \
        --configuration Release \
        --framework $FRAMEWORK_BUILD \
        --runtime $ARCHITECTURE_BUILD \
        --no-self-contained \
        --output "$BUILD_FUNCTION_FOLDER" \
        -property:GenerateRuntimeConfigurationFiles=true \
        -property:TieredCompilation=$TIERED_BUILD \
        -property:TieredCompilationQuickJit=$TIERED_BUILD \
        -property:PublishReadyToRun=$READY2RUN_BUILD \
        "$PROJECTS_FOLDER/$1/" > "$LOG_FILE"

    # check if the build was successful
    if [ -d $BUILD_FUNCTION_FOLDER ]; then
        if [ "$(ls -A $BUILD_FUNCTION_FOLDER)" ]; then
            local ZIP_FILE="$PUBLISH_FOLDER/$FUNCTION_LABEL.zip"

            # compress build output into zip file
            pushd "$BUILD_FUNCTION_FOLDER" > /dev/null
            zip -9 -r "$ZIP_FILE" . > /dev/null
            popd > /dev/null
            local ZIP_SIZE="$(wc -c <"$ZIP_FILE")"
            echo "==> Success: $ZIP_SIZE bytes"

            # copy JSON file and add runtime/architecture details
            local RUNSPEC_OUTPUT="$PUBLISH_FOLDER/$FUNCTION_LABEL.json"
            cat "$PROJECTS_FOLDER/$1/RunSpec.json" | jq ". += {\"Project\":\"$1\",\"Runtime\":\"$2\",\"Architecture\":\"$3\",\"ZipSize\":$ZIP_SIZE,\"Tiered\":\"$4\",\"Ready2Run\":\"$5\"}" > "$RUNSPEC_OUTPUT"
        else

            # show build output and delete empty folder
            cat "$PUBLISH_FOLDER/$FUNCTION_LABEL.log"
            rm -rf "$BUILD_FUNCTION_FOLDER"
        fi
    fi
}

# MinimalFunction
build_function MinimalFunction dotnet6 x86_64 no no
build_function MinimalFunction dotnet6 x86_64 no yes
build_function MinimalFunction dotnet6 x86_64 yes no
build_function MinimalFunction dotnet6 x86_64 yes yes
build_function MinimalFunction dotnet6 arm64 no no
build_function MinimalFunction dotnet6 arm64 no yes
build_function MinimalFunction dotnet6 arm64 yes no
build_function MinimalFunction dotnet6 arm64 yes yes
build_function MinimalFunction dotnetcore3.1 x86_64 no no
build_function MinimalFunction dotnetcore3.1 x86_64 no yes
build_function MinimalFunction dotnetcore3.1 x86_64 yes no
build_function MinimalFunction dotnetcore3.1 x86_64 yes yes
build_function MinimalFunction dotnetcore3.1 arm64 no no
build_function MinimalFunction dotnetcore3.1 arm64 no yes
build_function MinimalFunction dotnetcore3.1 arm64 yes no
build_function MinimalFunction dotnetcore3.1 arm64 yes yes

# NewtonsoftJson
build_function NewtonsoftJson dotnet6 x86_64 no no
build_function NewtonsoftJson dotnet6 x86_64 no yes
build_function NewtonsoftJson dotnet6 x86_64 yes no
build_function NewtonsoftJson dotnet6 x86_64 yes yes
build_function NewtonsoftJson dotnet6 arm64 no no
build_function NewtonsoftJson dotnet6 arm64 no yes
build_function NewtonsoftJson dotnet6 arm64 yes no
build_function NewtonsoftJson dotnet6 arm64 yes yes
build_function NewtonsoftJson dotnetcore3.1 x86_64 no no
build_function NewtonsoftJson dotnetcore3.1 x86_64 no yes
build_function NewtonsoftJson dotnetcore3.1 x86_64 yes no
build_function NewtonsoftJson dotnetcore3.1 x86_64 yes yes
build_function NewtonsoftJson dotnetcore3.1 arm64 no no
build_function NewtonsoftJson dotnetcore3.1 arm64 no yes
build_function NewtonsoftJson dotnetcore3.1 arm64 yes no
build_function NewtonsoftJson dotnetcore3.1 arm64 yes yes

# SystemTextJson
build_function SystemTextJson dotnet6 x86_64 no no
build_function SystemTextJson dotnet6 x86_64 no yes
build_function SystemTextJson dotnet6 x86_64 yes no
build_function SystemTextJson dotnet6 x86_64 yes yes
build_function SystemTextJson dotnet6 arm64 no no
build_function SystemTextJson dotnet6 arm64 no yes
build_function SystemTextJson dotnet6 arm64 yes no
build_function SystemTextJson dotnet6 arm64 yes yes
build_function SystemTextJson dotnetcore3.1 x86_64 no no
build_function SystemTextJson dotnetcore3.1 x86_64 no yes
build_function SystemTextJson dotnetcore3.1 x86_64 yes no
build_function SystemTextJson dotnetcore3.1 x86_64 yes yes
build_function SystemTextJson dotnetcore3.1 arm64 no no
build_function SystemTextJson dotnetcore3.1 arm64 no yes
build_function SystemTextJson dotnetcore3.1 arm64 yes no
build_function SystemTextJson dotnetcore3.1 arm64 yes yes

# SourceGeneratorJson
build_function SourceGeneratorJson dotnet6 x86_64 no no
build_function SourceGeneratorJson dotnet6 x86_64 no yes
build_function SourceGeneratorJson dotnet6 x86_64 yes no
build_function SourceGeneratorJson dotnet6 x86_64 yes yes
build_function SourceGeneratorJson dotnet6 arm64 no no
build_function SourceGeneratorJson dotnet6 arm64 no yes
build_function SourceGeneratorJson dotnet6 arm64 yes no
build_function SourceGeneratorJson dotnet6 arm64 yes yes
