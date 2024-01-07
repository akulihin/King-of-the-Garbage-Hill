//impot addons
const https = require('https');
const fs = require('fs');
const util = require('util');
const domino = require("domino");
const OAuthClient = require('disco-oauth');
const server = require('express')();
const cookieParser = require('cookie-parser')
const helmet = require('helmet');
const axios = require('axios');
const path = require('path');
const bodyParser = require('body-parser')
const cors = require("cors");
//end impot addons


//apply cors settings
const corsOptions = {
  origin: '*',
  credentials: true,            //access-control-allow-credentials:true
  optionSuccessStatus: 200,
}
server.use(cors(corsOptions))
// parse application/x-www-form-urlencoded
server.use(bodyParser.urlencoded({ extended: false }))
// parse application/json
server.use(bodyParser.json())


//secure http header
server.use(
  helmet.contentSecurityPolicy({
    directives: {
      ...helmet.contentSecurityPolicy.getDefaultDirectives()
    },
  })
);
//end secure http header

//global
const live_data_folder_path = '../live_data/';
const max_tries = 5;
const cookie_parmas = { expires: new Date(Date.now() + 500000), httpOnly: true, secure: true, domain: 'd2lfg.ru', };
//end global


//logging
var logFile = fs.createWriteStream(`../logs/log-${new Date().toISOString().replace(/T.*/, '')}.txt`, { flags: 'a' });
var logStdout = process.stdout;

console.log = function () {
  const date = new Date().toISOString();
  logFile.write(`[${date}]    ` + util.format.apply(null, arguments) + '\n');
  logStdout.write(`[${date}]    ` + util.format.apply(null, arguments) + '\n');
}
console.error = console.log;
//end logging


//express logging
const getActualRequestDurationInMilliseconds = start => {
  const diff = process.hrtime(start);
  return (diff[0] * 1e9 + diff[1]) / 1e6;
};

let expressLogger = (req, res, next) => {
  const start = process.hrtime();
  const durationInMilliseconds = getActualRequestDurationInMilliseconds(start);
  const clientIp = req.headers['x-forwarded-for'] || req.connection.remoteAddress;
  let log = `${clientIp.replace('::ffff:', '')} - ${req.method}:${req.url} ${res.statusCode} ${durationInMilliseconds.toLocaleString()} ms`;
  console.log(log);
  next();
};
//end express logging


//use logging
server.use(expressLogger);
server.use(cookieParser())
//end use logging


//sleep
function sleep(ms) {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
//end sleep

//Discord configurations
const config_data = JSON.parse(fs.readFileSync('../secrets/config.json', 'utf8'));
const oauthClient = new OAuthClient(config_data['discord_client_id'], config_data['discord_client_secret']);
oauthClient.setScopes('identify');
oauthClient.setRedirect(config_data['domain'] + config_data['discord_callback']);
//end Discord configurations


//method that reads and saves new data in a safe manner
async function readWriteAsync(data, file_path) {
  var error = false;
  var existing_data = [];
  var existing_data_string = "[]";
  var tries = -1;


  do {
    error = false;

    try {
      existing_data = JSON.parse(fs.readFileSync(live_data_folder_path + file_path, 'utf8'));
    } catch (err) {
      console.log('Error parsing/reading JSON string:', err);
      error = true;
      await sleep(500);
    }

    tries += 1;
    if (tries >= max_tries) {
      return false;
    }
  } while (error);


  if (tries >= 1) {
    console.log(`Reading file took ${tries} tries`);
  }
  tries = -1;


  do {
    error = false;
    try {
      existing_data.push(data);
      console.log(`${file_path} count ${existing_data.length}`)
      existing_data_string = JSON.stringify(existing_data);
    } catch (err) {
      console.log('Error pushing new data:', err);
      error = true;
      await sleep(500);
    }
    tries += 1;
    if (tries >= max_tries) {
      return false;
    }
  } while (error);


  if (tries >= 1) {
    console.log(`Pushing data took ${tries} tries`);
  }
  tries = -1;


  do {
    error = false;
    try {
      fs.writeFileSync(live_data_folder_path + file_path, existing_data_string)
    }
    catch (err) {
      console.log('Error writing file', err);
      error = true;
      await sleep(500);
    }
    tries += 1;
    if (tries >= max_tries) {
      return false;
    }
  } while (error);


  if (tries >= 1) {
    console.log(`Writing to file took ${tries} tries`);
  }
  tries = -1;

  return true;
}
//end



const port = process.env.PORT || 4444;


server.options('/*', async (req, res) => {
  res.status(204).send()
})

server.get('/', async (req, res) => {
  res.send('Hello World!')
})

server.get('/api/profiles/mylorik', async (req, res) => {
  var response = {
    "profile": {
      "username": "mylorik",
      "bio": "asdasdsa",
      "image": "http://127.0.0.1:4444/images/smiley-cyrus.jpeg",
      "following": false
    }
  }
  res.send(response)
});

//login
server.post('/api/users/login', async (req, res) => {
  console.log(req.body); // the posted data
  var response = { "errors": { "email or password": ["is invalid"] } }
  response = {
    "user": {
      "email": "duraek@gmail.com",
      "username": "mylorik",
      "bio": "asdasdsa",
      "image": "http://127.0.0.1:4444/images/smiley-cyrus.jpeg",
      "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VyIjp7ImlkIjoyMTgzfSwiaWF0IjoxNzA0NjY0MDE2LCJleHAiOjE3MDk4NDgwMTZ9.bcbCnVLuVSXpU64aBsXQW2uOqJ8wUgwW-ZLvkSqbxWI"
    }
  }
  res.send(response)
});

//register
server.post('/api/users', async (req, res) => {
  console.log(req.body); // the posted data
  var response = { "errors": { "username": ["has already been taken"] } }
  response = {
    "user": {
      "email": "duraek@gmail.com",
      "username": "mylorik",
      "bio": "asdasdsa",
      "image": "http://127.0.0.1:4444/images/smiley-cyrus.jpeg",
      "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VyIjp7ImlkIjoyMTgzfSwiaWF0IjoxNzA0NjY0MDE2LCJleHAiOjE3MDk4NDgwMTZ9.bcbCnVLuVSXpU64aBsXQW2uOqJ8wUgwW-ZLvkSqbxWI"
    }
  }
  res.send(response)
});


//Discord auth page
server.get("/auth/discord", async (req, res) => {
  try {
    const { link, state } = oauthClient.auth;
    res.cookie('user-state', state);
    res.redirect(link);
  } catch (error) {
    console.error(error);
    res.redirect('/error');
  }
});



/*
ref: https://disco-oauth.vercel.app/guide/authentication
*/
server.get("/auth/discord/authenticate", async (req, res) => {
  try {
    if (req.query.state && req.query.code && req.cookies['user-state']) {
      if (req.query.state === req.cookies['user-state']) {
        const userKey = await oauthClient.getAccess(req.query.code).catch(console.error);
        res.cookie('user-state', 'deleted', { maxAge: -1 });
        res.cookie('user-key', userKey);

        const discord_data = await oauthClient.getUser(userKey);
        const clientIp = req.headers['x-forwarded-for'] || req.connection.remoteAddress;
        var result = await readWriteAsync({ "ip": clientIp.replace('::ffff:', ''), "discordid": discord_data['_id'] }, 'discord_live.json');

        if (result) {
          res.cookie('discord_id', discord_data['_id'], cookie_parmas);
          res.send('/authorization-successful');
        }
        else {
          res.send('/authorization-failed');
        }

      } else {
        res.send('States do not match. Nice try hackerman!');
      }
    } else {
      res.send('Invalid login request.');
    }
  } catch (error) {
    console.error(error);
    res.send('/error');
  }
});


server.get("/favorite_track", async (req, res) => {
  try {
    res.redirect('https://youtu.be/dQw4w9WgXcQ');

  } catch (error) {
    console.error(error);
    res.redirect('https://youtu.be/dQw4w9WgXcQ');
  }
});


server.get('/*', async (req, res) => {
  res.send('*')
})

server.post('/*', async (req, res) => {
  console.log(req.body); // the posted data
  res.send('*')
})


var httpsServer = https.createServer({
  key: fs.readFileSync('../secrets/d2lfg.ru.key'),
  cert: fs.readFileSync('../secrets/d2lfg.ru.crt')
}, server);


//http
server.listen(port, () => {
  console.log(`Example app listening on port ${port}`)
})

/*
//https
httpsServer.listen(port, () => {
  console.log(`Example app listening on port ${port}`)
})
*/