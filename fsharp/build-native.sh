#!/usr/bin/env bash

setup_dirs() 
{
    echo Setting up directory for build

    mkdir -p "$__nativeroot"
    cp -r $__nativesrcroot $__nativetmproot
}

build_native()
{
    # All set to commence the build

    echo "Commencing build of mudt native components for $__BuildOS.$__BuildArch.$__BuildType"
    cd "$__nativetmproot"

    # Regenerate the CMake solution
    echo "Invoking cmake with arguments: \"$__nativetmproot\" $__CMakeArgs $__CMakeExtraArgs"
    "$__nativetmproot/gen-buildsys-clang.sh" "$__nativetmproot" $__ClangMajorVersion $__ClangMinorVersion $__BuildArch $__CMakeArgs "$__CMakeExtraArgs"

    # Check that the makefiles were created.

    if [ ! -f "$__nativetmproot/Makefile" ]; then
        echo "Failed to generate native component build project!"
        exit 1
    fi

    # Build

    echo "Executing make install -j $__NumProc $__MakeExtraArgs"

    make install -j $__NumProc $__MakeExtraArgs
    if [ $? != 0 ]; then
        echo "Failed to build mudt native components."
        exit 1
    fi
}

post_build_native()
{
    mv $__nativetmproot/MUDT.Native.Apple.dylib $__nativeroot/MUDT.Native.Apple.dylib
    mv $__nativetmproot/MUDT.Native.Linux.dylib $__nativeroot/MUDT.Native.Linux.dylib

    rm -r $__nativetmproot
}

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)
__nativesrcroot="$__scriptpath/src/Native"
__rootRepo="$__scriptpath/../.."
__nativetmproot="$__scriptpath/.native-tmp"
__nativeroot="$__scriptpath/.native"

# Set the various build properties here so that CMake and MSBuild can pick them up
__CMakeExtraArgs=""
__MakeExtraArgs=""
__generateversionsource=false
__BuildArch=x64
__BuildType=Debug
__CMakeArgs=DEBUG
__BuildOS=Linux
__NumProc=1
__UnprocessedBuildArgs=
__CrossBuild=0
__ServerGC=0
__VerboseBuild=false
__ClangMajorVersion=3
__ClangMinorVersion=5
__StaticLibLink=0
__PortableLinux=0

CPUName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [ $CPUName == "unknown" ]; then
    CPUName=$(uname -m)
fi

if [ $CPUName == "i686" ]; then
    __BuildArch=x86
fi

while :; do
    if [ $# -le 0 ]; then
        break
    fi

    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -\?|-h|--help)
            usage
            exit 1
            ;;
        x86)
            __BuildArch=x86
            ;;
        x64)
            __BuildArch=x64
            ;;
        arm)
            __BuildArch=arm
            ;;
        arm-softfp)
            __BuildArch=arm-softfp
            ;;
        arm64)
            __BuildArch=arm64
            ;;
        debug)
            __BuildType=Debug
            ;;
        release)
            __BuildType=Release
            __CMakeArgs=RELEASE 
            ;;
        freebsd)
            __BuildOS=FreeBSD
            ;;
        linux)
            __BuildOS=Linux
            ;;
        netbsd)
            __BuildOS=NetBSD
            ;;
        osx)
            __BuildOS=OSX
            ;;
        darwin)
            __BuildOS=OSX
            ;;
        --numproc)
            shift
            __NumProc=$1
            ;;         
        verbose)
            __VerboseBuild=1
            ;;
        staticliblink)
            __StaticLibLink=1
            ;;
        portablelinux)
            __PortableLinux=1
            ;;
        generateversion)
            __generateversionsource=true
            ;;
        clang3.5)
            __ClangMajorVersion=3
            __ClangMinorVersion=5
            ;;
        clang3.6)
            __ClangMajorVersion=3
            __ClangMinorVersion=6
            ;;
        clang3.7)
            __ClangMajorVersion=3
            __ClangMinorVersion=7
            ;;
        clang3.8)
            __ClangMajorVersion=3
            __ClangMinorVersion=8
            ;;
        clang3.9)
            __ClangMajorVersion=3
            __ClangMinorVersion=9
            ;;
        cross)
            __CrossBuild=1
            ;;
        cmakeargs)
            if [ -n "$2" ]; then
                __CMakeExtraArgs="$__CMakeExtraArgs $2"
                shift
            else
                echo "ERROR: 'cmakeargs' requires a non-empty option argument"
                exit 1
            fi
            ;;
        makeargs)
            if [ -n "$2" ]; then
                __MakeExtraArgs="$__MakeExtraArgs $2"
                shift
            else
                echo "ERROR: 'makeargs' requires a non-empty option argument"
                exit 1
            fi
            ;;
        useservergc)
            __ServerGC=1
            ;;
        *)
            __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac

    shift
done

__CMakeExtraArgs="$__CMakeExtraArgs -DCMAKE_INSTALL_PREFIX=$__nativetmproot"

# Make the directories necessary for build if they don't exist
setup_dirs

# Build the mudt native components.

build_native

# Move dylibs to more permanent directory

post_build_native
