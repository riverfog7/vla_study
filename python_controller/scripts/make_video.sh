#!/usr/bin/env bash

set -euo pipefail

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 <artifact_directory> [output_video_path] [fps]"
    exit 1
fi

IMG_EXTS=("jpg", "jpeg", "png", "bmp", "tiff")
ARTIFACT_DIR=${1}
OUTPUT_VIDEO=${2:-"${ARTIFACT_DIR}/output_video.mp4"}
FPS=${3:-5}
TMPFILE="./tmp_file_list.txt"

for img in $(find ${ARTIFACT_DIR} -type f \( -iname "*.jpg" -o -iname "*.jpeg" -o -iname "*.png" -o -iname "*.bmp" -o -iname "*.tiff" \) | sort); do
    echo "file '${img}'" >> ${TMPFILE}
done

ffmpeg -f concat -safe 0 -i ${TMPFILE} -r ${FPS} -pix_fmt yuv420p -c:v h264 ${OUTPUT_VIDEO}
rm ${TMPFILE}
