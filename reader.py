import time

class Reader():
    def __init__(self, ser) -> None:
        """
        RFID reader object
        """
        self.ser = ser


    def read(self):
        """
        Read from serial interface. 

        return: string hex representation of 16 bit words
        note - if 0xE280 is sent, add E2 and 80 end to end bitwise and that will
        represent the word in MSB first format
        """
        # wait for \n response
        while self.ser.readline() != b'\n':
                time.sleep(0.0001)
        # read response
        data = self.ser.readline()
        # decode from bytes to characters
        decoded = data.decode()
        # remove <CR><LF>
        return decoded.replace('\r\n', '')
    
    def hex_str_to_int_list(self, input_string, reversed:bool=False):
        """
        input_string: a string of hex words in MSB first format. EX: 'E2801160' for a two word string
        reversed: if True, output will be LSB first

        return: words (int) - MSB or LSB first depending on value of 'reversed'
        """
        if len(input_string) >=4:
            # split into hex strings
            hex_list = [input_string[i:i+4] for i in range(0, len(input_string), 4)]
            # convert to ints
            int_values = list(map(lambda x: (int(x[0:2], 16) << 8) | int(x[2:4], 16), hex_list))
            if reversed:
                return list(map(lambda x: int(bin(x)[2:].zfill(16)[::-1],2), int_values))
            else:
                return list(map(lambda x: int(bin(x)[2:].zfill(16),2), int_values))
        else:
            return False

    def hex_str_to_bin_list(self, input_string, reversed:bool=False):
        """
        input_string: a string of hex words in MSB first format. EX: 'E2801160' for a two word string
        reversed: if True, output will be LSB first

        return: words (int) - MSB or LSB first depending on value of 'reversed'
        """
        if len(input_string) >=4:
        # split into hex strings
            hex_list = [input_string[i:i+4] for i in range(0, len(input_string), 4)]
            # convert to ints
            int_values = list(map(lambda x: (int(x[0:2], 16) << 8) | int(x[2:4], 16), hex_list))
            if reversed:
                return list(map(lambda x: bin(x)[2:].zfill(16)[::-1], int_values))
            else:
                return list(map(lambda x: bin(x)[2:].zfill(16), int_values))
        else:
            return False

    def convert_to_raw(self, input_int_list):
        """
        converts MSB first int list to large raw binary blob 

        input_int_list: MSB first word list

        return: binary string padded to 96 bits long (LSB in element 0)
        """
        # convert to bin
        output_binary_string = ''
        for i in range(len(input_int_list)):
            output_binary_string += bin(input_int_list[i])[2:].zfill(16)
        return output_binary_string

    def read_TID_bank(self, addr:int=0, words:int=6, raw:bool=False):
        """
        Read lower 96 bytes from TID bank (Bank 2)

        (lower 48 and extended)

        addr: Starting address
        len: Words to read from tag
        raw: Returns the hex string if true

        return: LSB first converted ints (default)
                HEX string if raw is set to True
        """
        to_write = "\nR2,{},{}\r".format(addr,words).encode('utf-8')
        self.ser.write(to_write)
        # read and replace the "R" command readback byte
        string_response =  self.read().replace('R', '').encode('utf-8')
        # if no tag read, return False
        if len(string_response) <= 2:
            return False
        if raw:
            return string_response
        else:
            return self.hex_str_to_int_list(string_response)


    def reader_ID(self):
        """
        Return the reader ID
        """
        self.ser.write(b'\nS\r')
        return self.read()