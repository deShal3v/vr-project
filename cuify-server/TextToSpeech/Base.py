class Text2Speech:
    def __init__(self, ):
        pass

    # Abstract method for text to speech
    def forward_(self, text):
        return None    

    # Check the output audio format and convert the format if necessary
    def forward(self, text):
        return self.forward_(text)
    
    # Abstract method for text to speech to file
    def forward_to_file_(self, text, file_path):
        pass

    # Check the output audio format and convert the format if necessary then save to file
    def forward_to_file(self, text, file_path):
        self.forward_to_file_(text, file_path)

    def __name__(self):
        return self.__class__.__name__