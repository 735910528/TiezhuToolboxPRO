# Packet capture helper for Epic Seven gear export.
# Requires: Python 3.x, Npcap, and `pip install scapy`
from scapy.all import *
import os
import sys
import threading

acks = {}
loads = {}


def try_buffer(curr_ack):
    buffers = sorted(acks[curr_ack], key=lambda x: x["seq"])
    final_buffer = b""
    for item in buffers:
        final_buffer += item["data"]

    try:
        hex_str = final_buffer.hex()
    except Exception:
        hex_str = "".join(x.encode("hex") for x in final_buffer)

    print(hex_str)
    print("&")
    sys.stdout.flush()


def check_packet(packet):
    if IP not in packet:
        return
    if Raw not in packet or not packet[Raw].load:
        return

    curr_ack = packet.ack
    packet_bytes = bytes(packet[Raw].load)
    packet_hex = packet_bytes.hex()
    if packet_hex in loads:
        return
    loads[packet_hex] = True

    entry = {"data": packet_bytes, "seq": packet[TCP].seq}
    if curr_ack in acks:
        acks[curr_ack].append(entry)
    else:
        acks[curr_ack] = [entry]


def terminate():
    os._exit(0)


def thread_sniff():
    try:
        sniff(
            iface=get_working_ifaces(),
            prn=lambda x: check_packet(x),
            filter="tcp and ( port 5222 or port 3333 )",
            session=TCPSession,
        )
    except Exception:
        pass


worker = threading.Thread(target=thread_sniff)
worker.daemon = True
worker.start()

threading.Timer(3600.0, terminate).start()

while True:
    line = sys.stdin.readline()
    if not line:
        break
    if "E" in line:
        for ack in list(acks):
            try_buffer(ack)
        print("DONE\n")
        sys.stdout.flush()
        break
