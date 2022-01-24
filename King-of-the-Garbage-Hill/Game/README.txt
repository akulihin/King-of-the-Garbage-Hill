Copy the Game:
cd /mnt/d/git/King-of-the-Garbage-Hill/King-of-the-Garbage-Hill/bin/Debug/;rm -rf net6.0-prod;cp -R net6.0 net6.0-prod;rm -rf net6.0-prod/DataBase; tar -zcvf king-prod.tar.gz net6.0-prod/;scp king-prod.tar.gz 18.157.248.86:~;cd ../../DataBase; scp characters.json 18.157.248.86:~;
ssh 18.157.248.86
cd ~; mv characters.json king/net6.0-prod/DataBase/; tar -xzvf king-prod.tar.gz -C king; sudo systemctl restart king-daemon.service; less +F ~/king/net6.0-prod/DataBase/Log-1-23-2022.log







