from io import BufferedReader, BufferedWriter
import os

class IoHelper:
    @staticmethod
    def open_read(file_path: str) -> BufferedReader:
        return open(IoHelper.make_safe_file_path(file_path), "rb")

    @staticmethod
    def open_write(file_path: str) -> BufferedWriter:
        return open(IoHelper.make_safe_file_path(file_path), "wb")

    @staticmethod
    def make_safe_file_path(file_path: str) -> str:
        if "\\" not in file_path:
            return file_path

        return "\\\\?\\" + os.path.abspath(file_path)
