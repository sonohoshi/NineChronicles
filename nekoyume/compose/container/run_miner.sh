#!/bin/bash

PORT=$1
SEED_PORT=$2

sleep 5
/nekoyume/nekoyume --host host.docker.internal --port "$PORT" --peer "032307a4ea8b042a0805e3852010d5138c6fb4799eb1c216c509849208229b06f3,host.docker.internal,$SEED_PORT,1" --console-sink | tee /root/.config/miner.log
