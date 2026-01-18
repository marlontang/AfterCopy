#!/bin/bash

#执行方式   ./git-push.sh 

# 添加SSH密钥
ssh-add --apple-use-keychain

# Git操作
git add .

# 获取提交信息
if [ -z "$1" ]; then
    echo "请输入提交信息:"
    read COMMIT_MSG
else
    COMMIT_MSG="$1"
fi

# 提交并推送
git commit -m "$COMMIT_MSG"
git push -u origin main

echo "代码已成功提交到GitHub！"
