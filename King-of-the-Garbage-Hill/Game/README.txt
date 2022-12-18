Copy the Game:
To Server
cd /mnt/f/git/King-of-the-Garbage-Hill/King-of-the-Garbage-Hill/bin/Debug/;cp ../../DataBase/characters.json net6.0/DataBase;rm -rf net6.0-prod;cp -R net6.0 net6.0-prod;rm -rf net6.0-prod/DataBase/UserAccounts;rm -rf net6.0-prod/DataBase/ServerAccounts;rm -rf net6.0-prod/DataBase/Log-*;rm -rf net6.0-prod/DataBase/GameLogs.json;tar -zcvf king-prod.tar.gz net6.0-prod/;scp king-prod.tar.gz 35.158.35.107:~;cd /mnt/f/git/King-of-the-Garbage-Hill/King-of-the-Garbage-Hill/DataBase/;scp characters.json 35.158.35.107:~;


On Server
cd ~; tar -xzvf king-prod.tar.gz -C king; sudo systemctl restart king-daemon.service; less +F ~/king/net6.0-prod/DataBase/Log-8-7-2022.log

